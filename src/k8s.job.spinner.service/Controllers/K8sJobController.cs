using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using k8s.job.spinner.service.MessagePackets;
using k8s.Models;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto.Tls;
using Steeltoe.Common;
using Steeltoe.Common.Kubernetes;

namespace k8s.job.spinner.service.Controllers
{
    public enum JobStatus
    {
        All =0,
        Success =1,
        Running = 2,
        Failed = 3

    }
    [Route("api/[controller]")]
    [ApiController]
    public class K8sJobController : ControllerBase
    {
        KubernetesApplicationOptions _appOptions;
        ILogger<k8sCronJobController> _logger;
        IKubernetes _k8sClient;
        public K8sJobController(IApplicationInstanceInfo appInst,
           ILogger<k8sCronJobController> logger, IKubernetes kubernetesClient)
        {
            _appOptions = appInst as KubernetesApplicationOptions;
            _k8sClient = kubernetesClient;
            _logger = logger;
        }
        /// <summary>
        /// Create a Job from existing jon
        /// </summary>
        /// <param name="request">ReqCreateJob</param>
        /// <returns>ResCreateFromJob</returns>
        [HttpPost]
        public async Task<ActionResult<ResCreateFromJob>> CreateJob(ReqCreateJob request)
        {
            if (string.IsNullOrWhiteSpace(request.ExistigJobName)) return new BadRequestObjectResult(new
            {
                Message = $"Existing cron name cannot be empty",
                Error_Code = 400
            });
            if (string.IsNullOrWhiteSpace(request.NewJobName)) return new BadRequestObjectResult(new
            {
                Message = $"new job name cannot be empty",
                Error_Code = 400
            });
            V1Container container;
            try
            {
                var nameSpace = string.IsNullOrWhiteSpace(request.NameSpace) ?
                                    _appOptions.NameSpace : request.NameSpace;
                var existingJob = await _k8sClient.ReadNamespacedJobAsync(request?.ExistigJobName, nameSpace);
                if (!string.IsNullOrEmpty(request?.ContainerName))
                    container = existingJob.Spec.Template.Spec.Containers?
                                .FirstOrDefault(cntair => cntair.Name.CompareTo(request?.ContainerName) == 0);
                else
                    container = existingJob.Spec.Template.Spec.Containers[0];

                if (request.Commands.Any())
                    container.Command = request.Commands;
                if (request.JobParameters.Any())
                    container.Args = request.JobParameters;
                var newJob = new V1Job
                {
                    ApiVersion = existingJob.ApiVersion,
                    Kind = existingJob.Kind,
                    //Metadata = existingJob.Metadata,
                    //Spec = existingJob.Spec
                };
                newJob.Metadata = new V1ObjectMeta();
                newJob.Metadata.Name = request?.NewJobName;
                newJob.Metadata.NamespaceProperty = nameSpace;
                newJob.Metadata.Labels = existingJob.Metadata.Labels;
                newJob.Spec = new V1JobSpec();
                newJob.Spec.TtlSecondsAfterFinished = existingJob.Spec.TtlSecondsAfterFinished;
                newJob.Spec.Template = new V1PodTemplateSpec();
          
                newJob.Spec.Template.Spec = new V1PodSpec();
                
                newJob.Spec.Template.Spec.Containers = existingJob.Spec.Template.Spec.Containers;
                newJob.Spec.Template.Spec.RestartPolicy = "OnFailure";
                var newJobCreationResult = await _k8sClient.CreateNamespacedJobAsync(newJob, nameSpace);
                var response = new ResCreateFromJob
                {
                    JobName = newJobCreationResult.Metadata.Name,
                    JobUid = newJobCreationResult.Metadata.Uid,
                    NameSpace = nameSpace,
                    JobType = newJob.Metadata.Labels["jobtype"],
                };
                _logger.LogInformation("New job created scuccessfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create a job from existing  job {ex.Message} \n {ex.StackTrace}");
                return StatusCode(500, new
                {
                    Message = $"Failed to create a job from existing  job {ex.Message}",
                    Error_Code = 500
                });
            }
        }
        /// <summary>
        /// Deletes the Job
        /// </summary>
        /// <param name="name">Name of the Job</param>
        /// <param name="ns">Name space if not provided defaults to namespace
        /// in which K8s jobspinner service is hosted</param>
        /// <returns>Delete response</returns>
        [HttpDelete]
        public async Task<ActionResult<ResDeleteJob>> DeleteJob(string name, string ns)
        {
            if (string.IsNullOrWhiteSpace(name)) return new BadRequestObjectResult(new
            {
                Message = $"Existing cron name cannot be empty",
                Error_Code = 400
            });
            try
            {
                var nameSpace = string.IsNullOrWhiteSpace(ns) ?
                                    _appOptions.NameSpace : ns;
                var delJob = await _k8sClient.ReadNamespacedJobAsync(name, nameSpace);
                
                var deleteResponse = await _k8sClient.DeleteNamespacedJobAsync(delJob.Metadata.Name,
                    delJob.Metadata.NamespaceProperty);
                var response = new ResDeleteJob
                {
                    IsDeleted = "deleted",
                    JobName = delJob.Metadata.Name,
                    JobType = delJob.Metadata.Labels.ContainsKey("jobtype")?
                                delJob.Metadata.Labels["jobtype"]:string.Empty,
                    JobUid = delJob.Metadata.Uid,
                    NameSpace = nameSpace

                };
                return Ok(response);

            }
            catch(Exception ex)
            {
                _logger.LogError($"Failed to Delete a job  {ex.Message} \n {ex.StackTrace}");
                return StatusCode(500, new
                {
                    Message = $"Failed to Delete a job {ex.Message}",
                    Error_Code = 500
                });
            }
        }
        /// <summary>
        /// Gets job type based on the jobtype custome label. This label needs to be provided 
        /// during the creation of job
        /// </summary>
        /// <param name="ns">name space if not provided defaults to namespace
        /// in which K8s jobspinner service is hosted</param>
        /// <param name="jobType">value of job type to search</param>
        /// <returns>List of Jobs</returns>
        [HttpGet]
        [Route("GetJobsByType")]
        public async Task<ActionResult<List<ResJobDetails>>>GetJobsByType(string ns, string jobType)
        {
            if (string.IsNullOrWhiteSpace(jobType)) return new BadRequestObjectResult(new
            {
                Message = $"Existing Job Type cannot be empty",
                Error_Code = 400
            });
            try
            {
                var nameSpace = string.IsNullOrWhiteSpace(ns) ?
                                    _appOptions.NameSpace : ns;
                var labels = $"jobtype={jobType}";

                var jobs = await _k8sClient.ListNamespacedJobAsync(nameSpace, labelSelector: labels);
                var response = jobs?.Items.Select(V1Job =>  new ResJobDetails {
                    JobName = V1Job.Metadata.Name,
                    JobStatus = V1Job.Status.ToString(),
                    JobType=V1Job.Metadata.Labels["jobtype"],
                    JobUid= V1Job.Metadata.Uid,
                    NameSpace = nameSpace
                }).ToList();
                return Ok(response);
            }
            catch(Exception ex)
            {
                _logger.LogError($"Failed to List a job  {ex.Message} \n {ex.StackTrace}");
                return StatusCode(500, new
                {
                    Message = $"Failed to List a job {ex.Message}",
                    Error_Code = 500
                });
            }
        }
        /// <summary>
        /// Gets job by job name
        /// </summary>
        /// <param name="ns">name space if not provided defaults to namespace
        /// in which K8s jobspinner service is hosted</param>
        /// <param name="name"> name of the Job</param>
        /// <returns>Job details</returns>
        [HttpGet]
        [Route("GetJobByName")]
        public async Task<ActionResult<ResJobDetails>> GetJobByName(string ns, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return new BadRequestObjectResult(new
            {
                Message = $"Existing cron name cannot be empty",
                Error_Code = 400
            });
            try
            {
                var nameSpace = string.IsNullOrWhiteSpace(ns) ?
                                    _appOptions.NameSpace : ns;
                var job = await _k8sClient.ReadNamespacedJobAsync(name, nameSpace);
                var response = new ResJobDetails
                {
                    JobName = job.Metadata.Name,
                    JobStatus = job.Status.ToString(),
                    JobType = job.Metadata.Labels.ContainsKey("jobtype") ?
                    job.Metadata.Labels["jobtype"]:string.Empty,
                    JobUid = job.Metadata.Uid,
                    NameSpace = nameSpace
                };
                return Ok(response);
            }
            catch(Exception ex)
            {
                _logger.LogError($"Failed to List a job by name  {ex.Message} \n {ex.StackTrace}");
                return StatusCode(500, new
                {
                    Message = $"Failed to List a job by name {ex.Message}",
                    Error_Code = 500
                });
            }
        }
        /// <summary>
        /// Gets job by job status
        /// </summary>
        /// <param name="ns">namespace of the job if not provided defaults to namespace
        /// in which K8s jobspinner service is hosted</param>
        /// <param name="js">job status as Failed, Active, success or all </param>
        /// <returns>Job list</returns>
        [HttpGet]
        [Route("GetJobsByStatus")]
        public async Task<ActionResult<List<ResJobDetails>>> GetJobsByStaus(string ns, JobStatus js)
        {
            try
            {
                var nameSpace = string.IsNullOrWhiteSpace(ns) ?
                                    _appOptions.NameSpace : ns;
                var filedSelector = string.Empty;
                filedSelector = js switch
                {
                    JobStatus.Success => "status.successful=1",
                    JobStatus.Failed or JobStatus.Running => "status.successful=0",
                    _ => string.Empty,
                };
                var jobs = await _k8sClient.ListNamespacedJobAsync(nameSpace, fieldSelector: filedSelector);
               
                var response = js switch
                {
                    JobStatus.Failed=>jobs.Items.Where(v1Job=>((v1Job.Status.Failed>0)
                                                            && (v1Job.Status.Active==null))
                                                       ).Select(V1Job => new ResJobDetails
                                                {
                                                    JobName = V1Job.Metadata.Name,
                                                    JobStatus = V1Job.Status.ToString(),
                                                    JobType = V1Job.Metadata.Labels.ContainsKey("jobtype") ?
                                                              V1Job.Metadata.Labels["jobtype"] : string.Empty,
                                                    JobUid = V1Job.Metadata.Uid,
                                                    NameSpace = nameSpace
                                                }).ToList(),
                    JobStatus.Running=> jobs.Items.Where(v1Job => ((v1Job.Status.Active > 0)
                                                               && (v1Job.Status.Failed==null))
                                                        ).Select(V1Job => new ResJobDetails
                                            {
                                                JobName = V1Job.Metadata.Name,
                                                JobStatus = V1Job.Status.ToString(),
                                                JobType = V1Job.Metadata.Labels.ContainsKey("jobtype") ?
                                                          V1Job.Metadata.Labels["jobtype"] : string.Empty,
                                                JobUid = V1Job.Metadata.Uid,
                                                NameSpace = nameSpace
                                            }).ToList(),
                    _ => jobs?.Items.Select(V1Job => new ResJobDetails
                    {
                        JobName = V1Job.Metadata.Name,
                        JobStatus = V1Job.Status.ToString(),
                        JobType = V1Job.Metadata.Labels.ContainsKey("jobtype") ?
                                  V1Job.Metadata.Labels["jobtype"] : string.Empty,
                        JobUid = V1Job.Metadata.Uid,
                        NameSpace = nameSpace
                    }).ToList()
                };
                
                return Ok(response);
            }
            catch(Exception ex)
            {
                _logger.LogError($"Failed to List a job by Status  {ex.Message} \n {ex.StackTrace}");
                return StatusCode(500, new
                {
                    Message = $"Failed to List a job by Status {ex.Message}",
                    Error_Code = 500
                });
            }
        }
        /// <summary>
        /// Get all the jobs in namespace
        /// </summary>
        /// <param name="ns">namespace name if not provided defaults to namespace
        /// in which K8s jobspinner service is hosted</param>
        /// <returns></returns>
        [HttpGet]
        [Route("GetAllJobs")]
        public async Task<ActionResult<List<ResJobDetails>>> GetAllJobs(string ns)
        {
            var nameSpace = string.IsNullOrWhiteSpace(ns) ?
                                    _appOptions.NameSpace : ns;
            try
            {
                
                var jobs = await _k8sClient.ListNamespacedJobAsync(nameSpace);
                var results = jobs?.Items.Select(V1Job => new ResJobDetails
                {
                    JobName = V1Job.Metadata.Name,
                    
                    JobType = V1Job.Metadata.Labels.ContainsKey("jobtype") ?
                              V1Job.Metadata.Labels["jobtype"] : string.Empty,
                    JobUid = V1Job.Metadata.Uid,
                    NameSpace = nameSpace
                }).ToList();
                foreach (var job in jobs.Items)
                {
                    var resultJob = results.FirstOrDefault(resJob => 
                                    resJob.JobName.CompareTo(job.Metadata.Name) == 0);
                    if (job.Status.Failed >0)
                    {
                        resultJob.JobStatus = "Failed";
                    }
                    else if (job.Status.Active>0)
                    {
                        resultJob.JobStatus = "Active";
                    }
                    else if (job.Status.Succeeded>0) resultJob.JobStatus = "Success";

                }
                return Ok(results);
            }
            catch(Exception ex)
            {
                _logger.LogError($"Failed to List a job in a nameSpace {nameSpace}  {ex.Message} \n {ex.StackTrace}");
                return StatusCode(500, new
                {
                    Message = $"Failed to List a job in a namespace {nameSpace} {ex.Message}",
                    Error_Code = 500
                });
            }
        }
    }
    
}

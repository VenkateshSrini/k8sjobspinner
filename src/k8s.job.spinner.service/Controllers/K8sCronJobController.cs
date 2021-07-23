using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using k8s.job.spinner.service.MessagePackets;
using k8s.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto.Tls;
using Steeltoe.Common;
using Steeltoe.Common.Kubernetes;

namespace k8s.job.spinner.service.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class k8sCronJobController : ControllerBase
    {
        KubernetesApplicationOptions _appOptions;
        ILogger<k8sCronJobController> _logger;
        IKubernetes _k8sClient;

        public k8sCronJobController(IApplicationInstanceInfo appInst,
            ILogger<k8sCronJobController> logger, IKubernetes kubernetesClient)
        {
            _appOptions = appInst as KubernetesApplicationOptions;
            _k8sClient = kubernetesClient;
            _logger = logger;
        }
        /// <summary>
        /// A post method to create a Job from existing cron job
        /// </summary>
        /// <param name="request">ReqCreateFromCronJob</param>
        /// <returns>ResCreateFromJob</returns>
        [HttpPost]
        public async Task<ActionResult<ResCreateFromJob>> CreateJob(ReqCreateFromCronJob request)
        {
           
            
            if (string.IsNullOrWhiteSpace(request.ExistigCronJobName)) return new BadRequestObjectResult(new
            {
                Message = $"Existing cron name cannot be empty",
                Error_Code = 400
            });
            if (string.IsNullOrWhiteSpace(request.NewCronJobName)) return new BadRequestObjectResult(new
            {
                Message = $"new job name cannot be empty",
                Error_Code = 400
            });
            V1Container container;
            try
            {
                var nameSpace = string.IsNullOrWhiteSpace(request.NameSpace) ?
                                _appOptions.NameSpace : request.NameSpace;
                var existinCron = await _k8sClient.ReadNamespacedCronJobAsync(
                                    request?.ExistigCronJobName, nameSpace);
                if (!string.IsNullOrEmpty(request?.ContainerName))
                    container = existinCron.Spec.JobTemplate.Spec
                                 .Template.Spec
                                 .Containers?.FirstOrDefault(cntair => cntair.Name.CompareTo(request?.ContainerName) == 0);
                else
                    container = existinCron.Spec.JobTemplate.Spec.Template.Spec.Containers[0];
                if (request.Commands.Any())
                    container.Command = request.Commands;
                if (request.JobParameters.Any())
                    container.Args = request.JobParameters;
                var newJob = new V1Job
                {
                    Metadata = new V1ObjectMeta
                    {
                        Name = request?.NewCronJobName,
                        NamespaceProperty = nameSpace,
                        Labels = existinCron.Metadata.Labels,
                        Annotations = existinCron.Metadata.Annotations

                    },
                    Spec = existinCron.Spec.JobTemplate.Spec
                };
                var newJobCreationResult = await _k8sClient.CreateNamespacedJobAsync(newJob, nameSpace);
                var response = new ResCreateFromJob
                {
                    JobName = newJobCreationResult.Metadata.Name,
                    JobUid = newJobCreationResult.Metadata.Uid,
                    NameSpace = nameSpace
                };
                _logger.LogInformation("New job created scuccessfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create a job from existing cron job {ex.Message} \n {ex.StackTrace}");
                return StatusCode(500, new
                {
                    Message = $"Failed to create a job from existing cron job {ex.Message}",
                    Error_Code = 500
                });
            }

        }
    }
}

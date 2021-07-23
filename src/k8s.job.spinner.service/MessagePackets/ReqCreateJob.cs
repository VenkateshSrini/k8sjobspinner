using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace k8s.job.spinner.service.MessagePackets
{
    public class ReqCreateJob
    {
        /// <summary>
        /// Existing  job name
        /// </summary>
        public string ExistigJobName { get; set; }
        /// <summary>
        /// Newer instance of  job
        /// </summary>
        public string NewJobName { get; set; }
        /// <summary>
        /// The namespace in which the cron job exist
        /// </summary>
        public string NameSpace { get; set; }
        /// <summary>
        /// Job parameters in case it is different for the new job. Other wise it will be an empty array
        /// </summary>
        public List<string> JobParameters { get; set; } = new List<string>();
        /// <summary>
        /// The command that needs to be executed in the new job container. If no change then leave it blank empty array
        /// </summary>
        public List<string> Commands { get; set; } = new List<string>();
        /// <summary>
        /// Name of the container for which the job parameters and command.
        /// This is useful in case the pod is deployed with sidecar.
        /// If there is only one contaniner then leave it blank.By default 
        /// first container will be considered.
        /// </summary>
        public string ContainerName { get; set; }
    }
}

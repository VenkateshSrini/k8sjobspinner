using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace k8s.job.spinner.service.MessagePackets
{
    public class ResJobDetails
    {
        /// <summary>
        /// Namespace in which the new job is created
        /// </summary>
        public string NameSpace { get; set; }
        /// <summary>
        /// Uid of the job that was created
        /// </summary>
        public string JobUid { get; set; }
        /// <summary>
        /// Name of the newly created job
        /// </summary>
        public string JobName { get; set; }
        /// <summary>
        /// Job type is a general label classification that can be used
        /// to identify the job type like Death engine, Perform transfer
        /// </summary>
        public string JobType { get; set; }
        public string JobStatus { get; set; }
    }
}

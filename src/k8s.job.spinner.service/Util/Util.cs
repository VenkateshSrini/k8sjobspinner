using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace k8s.job.spinner.service.Util
{
    public static class ConfigUtil
    {
        public static KubernetesClientConfiguration GetKubernetesClientConfiguration(string appRunIn)
        {
            if (appRunIn.ToLower() == "local")
                return KubernetesClientConfiguration.BuildConfigFromConfigFile();
            else
                return KubernetesClientConfiguration.InClusterConfig();
        }
    }
}

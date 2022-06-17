using System.Collections.ObjectModel;
using System.Fabric.Description;

namespace GameWeb
{
    public class MetricsCollection : KeyedCollection<string, ServiceLoadMetricDescription>
    {
        protected override string GetKeyForItem(ServiceLoadMetricDescription metricDescription)
        {
            return metricDescription.Name;
        }
    }
}

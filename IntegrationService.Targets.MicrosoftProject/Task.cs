using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IntegrationService.Targets.MicrosoftProject
{
    public class Task
    {
        public string Name { get; set; }
        public string Notes { get; set; }
        public int UniqueId { get; set; }
        public DateTime? Start { get; set; }
        public DateTime? BaselineStart { get; set; }
        public DateTime? EarlyStart { get; set; }
        public DateTime? Finish { get; set; }
        public DateTime? BaselineFinish { get; set; }
        public DateTime? EarlyFinish { get; set; }
        public bool Summary { get; set; }
        public bool Milestone { get; set; }
        public List<Task> ChildTasks { get; set; }
        public string Hyperlink { get; set; }
        public List<ResourceAssignment> ResourceAssignments { get; set; }
        public string ResourceGroup { get; set; }
        public string[] Text { get; set; }
        public int Priority { get; set; }
        public float Cost { get; set; }
        public float BaselineCost { get; set; }
        public double Work { get; set; }
        public double BaselineWork { get; set; }

        public string GetText(int idx)
        {
            if (Text[idx] != null)
                return Text[idx];
            else
                return string.Empty;
        }
    }

    public class ResourceAssignment
    {
        public Resource Resource { get; set; }
    }

    public class Resource
    {
        public string EmailAddress { get; set; }
    }
}

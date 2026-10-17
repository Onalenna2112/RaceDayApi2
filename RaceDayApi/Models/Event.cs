using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaceDayApi.Models
{
    public class Event
    {
        public int EventID { get; set; }
        public int OrganiserID { get; set; }
        public Organiser? Organiser { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public string Location { get; set; } = string.Empty;
        public decimal Distance { get; set; }
        public string EventType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Category> Categories { get; set; } = new List<Category>();
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}
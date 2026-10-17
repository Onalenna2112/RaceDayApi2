using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaceDayApi.Models
{
    public class Enrollment
    {
        public int EnrollmentID { get; set; }
        public int ParticipantID { get; set; }
        public Participant? Participant { get; set; }

        public int EventID { get; set; }
        public Event? Event { get; set; }

        public int CategoryID { get; set; }
        public Category? Category { get; set; }

        public DateTime EnrolmentDate { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Enrolled";

        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public Result? Result { get; set; }
    }
}
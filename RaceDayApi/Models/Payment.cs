using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaceDayApi.Models
{
    public class Payment
    {
        [Key]
        public int PaymentID { get; set; }

        [Required]
        public int EnrollmentID { get; set; }
        [ForeignKey("EnrollmentID")]
        public Enrollment Enrollment { get; set; } = null!;

        [Required]
        public decimal Amount { get; set; }

        [MaxLength(50)]
        public string PaymentStatus { get; set; } = "Pending";

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string TransactionReference { get; set; } = string.Empty;
    }

    public class Result
    {
        [Key]
        public int ResultID { get; set; }

        [Required]
        public int EnrollmentID { get; set; }
        [ForeignKey("EnrollmentID")]
        public Enrollment Enrollment { get; set; } = null!;

        [Required]
        public TimeSpan FinishTime { get; set; }

        [Required]
        public int Position { get; set; }

        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    }
}

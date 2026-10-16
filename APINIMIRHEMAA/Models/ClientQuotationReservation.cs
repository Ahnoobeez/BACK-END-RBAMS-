using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public sealed class ClientQuotationReservation
    {
        [Key]
        public Guid Reservation_ID { get; set; }
        public int Client_ID { get; set; }
        public int SequenceYear { get; set; }
        public byte SequenceMonth { get; set; }
        public int SequenceNumber { get; set; }
        [Required]
        [MaxLength(512)]
        public string Client_Subject { get; set; } = string.Empty;
        [Required]
        [MaxLength(200)]
        public string ReservedBy { get; set; } = string.Empty;
        public DateTime ReservedAt { get; set; }
        public bool IsUsed { get; set; }
    }
}

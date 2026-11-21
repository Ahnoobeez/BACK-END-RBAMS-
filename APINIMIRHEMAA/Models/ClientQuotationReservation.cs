using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the client quotation reservation record stored or used by the application.
    public sealed class ClientQuotationReservation
    {
        [Key]
        // Identifies the related reservation record.
        public Guid Reservation_ID { get; set; }
        // Identifies the related client record.
        public int Client_ID { get; set; }
        // Stores sequence year on this client quotation reservation.
        public int SequenceYear { get; set; }
        // Stores sequence month on this client quotation reservation.
        public byte SequenceMonth { get; set; }
        // Supplies the sequence number used to identify the related record.
        public int SequenceNumber { get; set; }
        [Required]
        [MaxLength(512)]
        // Stores client subject on this client quotation reservation.
        public string Client_Subject { get; set; } = string.Empty;
        [Required]
        [MaxLength(200)]
        // Stores reserved by on this client quotation reservation.
        public string ReservedBy { get; set; } = string.Empty;
        // Stores reserved at on this client quotation reservation.
        public DateTime ReservedAt { get; set; }
        // Indicates whether used applies to this client quotation reservation.
        public bool IsUsed { get; set; }
    }
}

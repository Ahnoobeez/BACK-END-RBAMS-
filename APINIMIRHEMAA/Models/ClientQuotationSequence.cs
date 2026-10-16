namespace APINIMIRHEMAA.Models
{
    public sealed class ClientQuotationSequence
    {
        public int Client_ID { get; set; }
        public int SequenceYear { get; set; }
        public byte SequenceMonth { get; set; }
        public int LastSequence { get; set; }
    }
}

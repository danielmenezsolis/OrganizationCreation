
namespace OrganizationCreation
{
    class CustomerComplete
    {
        #region Identificadores
        public long LocationId { get; set; }
        public string TaxId { get; set; }
        public long PartyId { get; set; }
        public long PartySiteId { get; set; }
        public int AccountNumber { get; set; }
        public long CustomerAccountId { get; set; }
        public bool CustomerProfileCreated { get; set; }
        #endregion
        #region Address
        public string Calle { get; set; }
        public string IntExt { get; set; }
        public string CodigoPostal { get; set; }
        public string Municipio { get; set; }
        public string Pais { get; set; }
        public string Estado { get; set; }
        public string Colonia { get; set; }
        public string NombreOrg { get; set; }
        public string RFCPF { get; set; }
        public string RFCPM { get; set; }
        #endregion
        #region Generales
        public string Nombre { get; set; }
        public string RFC { get; set; }
        public string Email { get; set; }
        public string Telefono { get; set; }
        public string Classification { get; set; }
        public string Subclassification { get; set; }
        public string ClientType { get; set; }
        public string Foreing { get; set; }
        #endregion
    }
}

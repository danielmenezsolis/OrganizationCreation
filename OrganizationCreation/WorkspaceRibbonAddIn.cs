using System;
using System.AddIn;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using OrganizationCreation.SOAPICCS;
using RightNow.AddIns.AddInViews;
using RightNow.AddIns.Common;

////////////////////////////////////////////////////////////////////////////////
//
// File: WorkspaceRibbonAddIn.cs
//
// Comments:
//
// Notes: 
//
// Pre-Conditions: 
//
////////////////////////////////////////////////////////////////////////////////
namespace OrganizationCreation
{
    public class WorkspaceRibbonAddIn : Panel, IWorkspaceRibbonButton
    {
        private bool InDesignMode;
        private IRecordContext RecordContext { get; set; }
        private IGlobalContext GlobalContext { get; set; }
        private IOrganization OrgRecord { get; set; }
        private RightNowSyncPortClient clientRN;

        

        public WorkspaceRibbonAddIn(bool inDesignMode, IRecordContext RecordContext, IGlobalContext global)
        {
            if (!inDesignMode)
            {
                this.GlobalContext = global;
                this.InDesignMode = inDesignMode;
                this.RecordContext = RecordContext;

                RecordContext.Saving += new CancelEventHandler(RecordContext_Saving);
            }
        }
        private void RecordContext_Saving(object sender, CancelEventArgs e)
        {
            try
            {
                Init();
                string validate = "";
                OrgRecord = RecordContext.GetWorkspaceRecord(WorkspaceRecordType.Organization) as IOrganization;
                CustomerComplete customerComplete = new CustomerComplete();
                IList<ICfVal> orgCustomFieldList = OrgRecord.CustomField;
                foreach (ICfVal orgcampos in orgCustomFieldList)
                {
                    if (orgcampos.CfId == 56)
                    {
                        validate = orgcampos.ValStr;
                    }
                }
                if (GlobalContext != null)
                {
                    if (String.IsNullOrEmpty(validate))
                    {
                        validate = "0";
                    }

                    if (validate == "0")
                    {
                        customerComplete.NombreOrg = OrgRecord.Name;
                        IList<IOrgAddr> list = OrgRecord.Oaddr;
                        foreach (IOrgAddr ad in list)
                        {
                            if (ad.OatID == 1)
                            {
                                customerComplete.Calle = ad.AddrStreet;
                                customerComplete.Colonia = ad.AddrCity;
                                customerComplete.CodigoPostal = ad.AddrPostalCode;
                                customerComplete.Pais = getIsoCode(Convert.ToInt32(ad.AddrCountryID));
                                customerComplete.Estado = getEstado(Convert.ToInt32(ad.AddrCountryID), Convert.ToInt32(ad.AddrProvID));

                            }
                        }
                        //Obtiene los valores
                        List<CustomFields> customFields = new List<CustomFields>();
                        foreach (ICfVal orgcampos in orgCustomFieldList)
                        {
                            CustomFields custom = new CustomFields();
                            custom.FieldDataType = GetDataType((int)orgcampos.DataType);
                            custom.FieldID = orgcampos.CfId;
                            custom.FieldName = GetName(orgcampos.CfId);


                            if ((int)orgcampos.DataType == 1)
                            {
                                custom.FieldValueInt = Convert.ToInt32(orgcampos.ValInt);

                                custom.FieldValueString = GetValue(orgcampos.CfId, Convert.ToInt32(orgcampos.ValInt));
                            }
                            if ((int)orgcampos.DataType == 2)
                            {
                                custom.FieldValueInt = (int)orgcampos.ValInt;
                                if (custom.FieldValueInt == 0)
                                { custom.FieldValueString = "No"; }
                                else
                                { custom.FieldValueString = "Yes"; }

                            }
                            if ((int)orgcampos.DataType == 5)
                            {
                                custom.FieldValueString = orgcampos.ValStr;
                            }
                            customFields.Add(custom);
                        }
                        foreach (CustomFields custom in customFields)
                        {
                            switch (custom.FieldID)
                            {
                                case 16:
                                    customerComplete.RFCPM = custom.FieldValueString;
                                    break;
                                case 17:
                                    customerComplete.TaxId = custom.FieldValueString;
                                    break;
                                case 18:
                                    customerComplete.Foreing = custom.FieldValueString;
                                    break;
                                case 19:
                                    customerComplete.Email = custom.FieldValueString;
                                    break;
                                case 20:
                                    customerComplete.Telefono = custom.FieldValueString;
                                    break;
                                case 21:
                                    customerComplete.ClientType = custom.FieldValueString;
                                    break;
                                case 23:
                                    customerComplete.RFCPF = custom.FieldValueString;
                                    break;
                                case 49:
                                    customerComplete.Classification = custom.FieldValueString;
                                    break;
                                case 50:
                                    customerComplete.Subclassification = custom.FieldValueString;
                                    break;
                            }
                        }
                        customerComplete.RFC = customerComplete.RFCPM;
                        if (customerComplete.ClientType == "Person")
                        {
                            customerComplete.RFC = customerComplete.RFCPF;
                        }
                        if (customerComplete.Foreing == "Yes")
                        {
                            customerComplete.RFC = customerComplete.TaxId;
                        }
                        customerComplete.RFC = String.IsNullOrEmpty(customerComplete.RFC) ? "." : customerComplete.RFC;
                        customerComplete = CallCreateLocation(customerComplete);
                        customerComplete = CallCreateOrganization(customerComplete);
                        customerComplete = CallCreateCustomerAccount(customerComplete);
                        customerComplete = CallCreateCustomerProfile(customerComplete);
                        if (customerComplete.CustomerProfileCreated)
                        {
                            MessageBox.Show("Cliente dado de alta en el ERP");
                        }
                        foreach (ICfVal orgcampos in orgCustomFieldList)
                        {

                            switch (orgcampos.CfId)
                            {
                                case 52:
                                    orgcampos.ValStr = customerComplete.LocationId.ToString();
                                    break;
                                case 53:
                                    orgcampos.ValStr = customerComplete.PartyId.ToString();
                                    break;
                                case 54:
                                    orgcampos.ValStr = customerComplete.PartySiteId.ToString();
                                    break;
                                case 55:
                                    orgcampos.ValStr = customerComplete.AccountNumber.ToString();
                                    break;
                                case 56:
                                    orgcampos.ValStr = customerComplete.CustomerAccountId.ToString();
                                    break;
                            }
                        }
                        //RecordContext.ExecuteEditorCommand(EditorCommand.Refresh);
                        RecordContext.RefreshWorkspace();
                    }
                }
                else
                {
                    MessageBox.Show("No hay global");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                throw ex;
            }
        }
        
        public new void Click()
        {
            try
            {
                OrgRecord = RecordContext.GetWorkspaceRecord(WorkspaceRecordType.Organization) as IOrganization;
                IList<IOrgAddr> list = OrgRecord.Oaddr;
                if (list != null)
                {
                    foreach (IOrgAddr ad in list)
                    {
                        if (ad.OatID == 1)
                        {
                            Init();
                            MessageBox.Show(getIsoCode(Convert.ToInt32(ad.AddrCountryID)));
                            MessageBox.Show(getEstado(Convert.ToInt32(ad.AddrCountryID), Convert.ToInt32(ad.AddrProvID)));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error en Click" + ex.Message);
            }

        }

        private CustomerComplete CallCreateLocation(CustomerComplete customerComplete)
        {
            try
            {
                GlobalContext.LogMessage("Crear Location");
                // Construct xml payload to invoke the service. In this example, it is a hard coded string.
                string envelope = "<soapenv:Envelope" +
                 "   xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"" +
                 "   xmlns:typ=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/locationService/applicationModule/types/\"" +
                 "   xmlns:loc=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/locationService/\"" +
                 "   xmlns:par=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/partyService/\"" +
                 "   xmlns:sour=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/flex/sourceSystemRef/\"" +
                 "   xmlns:loc1=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/flex/location/\">" +
                 "<soapenv:Header/>" +
                 "<soapenv:Body>" +
                 "<typ:createLocation>" +
                 "<typ:location>" +
                 "<loc:Country>" + customerComplete.Pais.ToUpper().Trim() + "</loc:Country>" +
                 "<loc:Address1>" + customerComplete.Calle.ToUpper() + "</loc:Address1>" +
                 "<loc:Address2></loc:Address2>" +
                 "<loc:City>" + customerComplete.Colonia.ToUpper() + "</loc:City>" +
                 "<loc:PostalCode>" + customerComplete.CodigoPostal + "</loc:PostalCode>" +
                 "<loc:State>" + customerComplete.Estado.ToUpper() + "</loc:State >" +
                 "<loc:Province>" + customerComplete.Colonia.ToUpper() + "</loc:Province>" +
                 "<loc:County>" + customerComplete.Colonia.ToUpper() + "</loc:County>" +
                 "<loc:CreatedByModule>HZ_WS</loc:CreatedByModule>" +
                 "</typ:location>" +
                 "</typ:createLocation>" +
                 "</soapenv:Body>" +
                 "</soapenv:Envelope>";

                byte[] byteArray = Encoding.UTF8.GetBytes(envelope);
                // Construct the base 64 encoded string used as credentials for the service call
                byte[] toEncodeAsBytes = System.Text.ASCIIEncoding.ASCII.GetBytes("itotal" + ":" + "Oracle123");
                string credentials = System.Convert.ToBase64String(toEncodeAsBytes);
                // Create HttpWebRequest connection to the service
                HttpWebRequest request =
                 (HttpWebRequest)WebRequest.Create("https://egqy-test.fa.us6.oraclecloud.com:443/crmService/FoundationPartiesLocationService");
                // Configure the request content type to be xml, HTTP method to be POST, and set the content length
                request.Method = "POST";
                request.ContentType = "text/xml;charset=UTF-8";
                request.ContentLength = byteArray.Length;
                // Configure the request to use basic authentication, with base64 encoded user name and password, to invoke the service.
                request.Headers.Add("Authorization", "Basic " + credentials);
                // Set the SOAP action to be invoked; while the call works without this, the value is expected to be set based as per standards
                request.Headers.Add("SOAPAction", "http://xmlns.oracle.com/apps/cdm/foundation/parties/locationService/applicationModule//LocationService/createLocation");
                // Write the xml payload to the request
                Stream dataStream = request.GetRequestStream();
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();

                // Write the xml payload to the request
                XDocument doc;
                XmlDocument docu = new XmlDocument();
                string result;
                // Get the response and process it; In this example, we simply print out the response XDocument doc;
                using (WebResponse response = request.GetResponse())
                {

                    using (Stream stream = response.GetResponseStream())
                    {
                        doc = XDocument.Load(stream);
                        result = doc.ToString();
                        XmlDocument xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(result);
                        XmlNamespaceManager nms = new XmlNamespaceManager(xmlDoc.NameTable);
                        nms.AddNamespace("env", "http://schemas.xmlsoap.org/soap/envelope/");
                        nms.AddNamespace("wsa", "http://www.w3.org/2005/08/addressing");
                        nms.AddNamespace("typ", "http://xmlns.oracle.com/apps/cdm/foundation/parties/locationService/applicationModule/types/");
                        nms.AddNamespace("ns2", "http://xmlns.oracle.com/apps/cdm/foundation/parties/locationService/applicationModule/types/");
                        nms.AddNamespace("ns1", "http://xmlns.oracle.com/apps/cdm/foundation/parties/locationService/");
                        XmlNode desiredNode = xmlDoc.SelectSingleNode("//ns1:Value", nms);
                        if (desiredNode.HasChildNodes)
                        {

                            for (int i = 0; i < desiredNode.ChildNodes.Count; i++)
                            {
                                if (desiredNode.ChildNodes[i].LocalName == "LocationId")
                                {
                                    string locationId = desiredNode.ChildNodes[i].InnerText;
                                    customerComplete.LocationId = (long)Convert.ToInt64(locationId);
                                    break;
                                }
                            }

                        }

                    }
                    response.Close();
                }
                return customerComplete;
            }

            catch (Exception ex)
            {
                MessageBox.Show("LocationNoCreado" + ex.Message);
                return customerComplete;
            }
        }
        private CustomerComplete CallCreateOrganization(CustomerComplete customerComplete)
        {
            try
            {
                GlobalContext.LogMessage("Crear Organization");
                string envelope = "<soapenv:Envelope" +
                    "   xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"" +
                    "   xmlns:typ=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/organizationService/applicationModule/types/\"" +
                    "   xmlns:org=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/organizationService/\"" +
                    "   xmlns:par=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/partyService/\"" +
                    "   xmlns:sour=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/flex/sourceSystemRef/\"" +
                    "   xmlns:con=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/contactPointService/\"" +
                    "   xmlns:con1=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/flex/contactPoint/\"" +
                    "   xmlns:org1=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/flex/organization/\"" +
                    "   xmlns:par1=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/flex/partySite/\"" +
                    "   xmlns:rel=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/relationshipService/\"" +
                    "   xmlns:org2=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/flex/orgContact/\"" +
                    "   xmlns:rel1=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/flex/relationship/\">" +
                    "<soapenv:Header/>" +
                    "<soapenv:Body>" +
                    "<typ:createOrganization>" +
                    "<typ:organizationParty>" +
                    "<org:CreatedByModule>HZ_WS</org:CreatedByModule>" +
                    "<org:Email>" +
                    "<con:CreatedByModule>HZ_WS</con:CreatedByModule>" +
                    "<con:EmailAddress>" + customerComplete.Email + "</con:EmailAddress>" +
                    "</org:Email>" +
                    "<org:Phone>" +
                    "<con:CreatedByModule>HZ_WS</con:CreatedByModule>" +
                    "<con:PhoneAreaCode>55</con:PhoneAreaCode>" +
                    "<con:PhoneCountryCode>52</con:PhoneCountryCode>" +
                    "<con:PhoneNumber>" + customerComplete.Telefono + "</con:PhoneNumber>" +
                    "</org:Phone>" +
                    "<org:PartyUsageAssignment>" +
                    "<par:CreatedByModule>HZ_WS</par:CreatedByModule>" +
                    "<par:PartyUsageCode>CUSTOMER</par:PartyUsageCode>" +
                    "</org:PartyUsageAssignment>" +
                    "<org:PartyUsageAssignment>" +
                    "<par:CreatedByModule>HZ_WS</par:CreatedByModule>" +
                    "<par:PartyUsageCode>SALES_ACCOUNT</par:PartyUsageCode>" +
                    "</org:PartyUsageAssignment>" +
                    "<org:PartySite>" +
                    "<par:CreatedByModule>HZ_WS</par:CreatedByModule>" +
                    "<par:LocationId>" + customerComplete.LocationId + "</par:LocationId>" +
                    "</org:PartySite>" +
                    "<org:OrganizationProfile>" +
                    "<org:CreatedByModule>HZ_WS</org:CreatedByModule>" +
                    "<org:OrganizationName>" + customerComplete.NombreOrg.ToUpper() + "</org:OrganizationName>" +
                    "<org:JgzzFiscalCode>" + customerComplete.RFC.ToUpper() + "</org:JgzzFiscalCode>" +
                    "</org:OrganizationProfile>" +
                    "</typ:organizationParty>" +
                    "</typ:createOrganization>" +
                    "</soapenv:Body>" +
                "</soapenv:Envelope>";
                byte[] byteArray = Encoding.UTF8.GetBytes(envelope);
                // Construct the base 64 encoded string used as credentials for the service call
                byte[] toEncodeAsBytes = System.Text.ASCIIEncoding.ASCII.GetBytes("itotal" + ":" + "Oracle123");
                string credentials = System.Convert.ToBase64String(toEncodeAsBytes);
                // Create HttpWebRequest connection to the service
                HttpWebRequest request =
                 (HttpWebRequest)WebRequest.Create("https://egqy-test.fa.us6.oraclecloud.com:443/crmService/FoundationPartiesOrganizationService");
                // Configure the request content type to be xml, HTTP method to be POST, and set the content length
                request.Method = "POST";
                request.ContentType = "text/xml;charset=UTF-8";
                request.ContentLength = byteArray.Length;
                // Configure the request to use basic authentication, with base64 encoded user name and password, to invoke the service.
                request.Headers.Add("Authorization", "Basic " + credentials);
                // Set the SOAP action to be invoked; while the call works without this, the value is expected to be set based as per standards
                request.Headers.Add("SOAPAction", "http://xmlns.oracle.com/apps/cdm/foundation/parties/organizationService/applicationModule/createOrganization");
                // Write the xml payload to the request
                Stream dataStream = request.GetRequestStream();
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();

                // Write the xml payload to the request
                XDocument doc;
                XmlDocument docu = new XmlDocument();
                string result;
                // Get the response and process it; In this example, we simply print out the response XDocument doc;
                using (WebResponse response = request.GetResponse())
                {

                    using (Stream stream = response.GetResponseStream())
                    {
                        doc = XDocument.Load(stream);
                        result = doc.ToString();
                        XmlDocument xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(result);
                        XmlNamespaceManager nms = new XmlNamespaceManager(xmlDoc.NameTable);
                        nms.AddNamespace("env", "http://schemas.xmlsoap.org/soap/envelope/");
                        nms.AddNamespace("wsa", "http://www.w3.org/2005/08/addressing");
                        nms.AddNamespace("typ", "http://xmlns.oracle.com/apps/cdm/foundation/parties/organizationService/applicationModule/types/");
                        nms.AddNamespace("ns2", "http://xmlns.oracle.com/apps/cdm/foundation/parties/organizationService/");
                        nms.AddNamespace("ns1", "http://xmlns.oracle.com/apps/cdm/foundation/parties/partyService/");
                        XmlNode desiredNode = xmlDoc.SelectSingleNode("//ns2:PartySite", nms);
                        if (desiredNode.HasChildNodes)
                        {
                            for (int i = 0; i < desiredNode.ChildNodes.Count; i++)
                            {

                                if (desiredNode.ChildNodes[i].LocalName == "PartySiteId")
                                {
                                    string partysiteId = desiredNode.ChildNodes[i].InnerText;
                                    customerComplete.PartySiteId = Convert.ToInt64(partysiteId);

                                }
                                if (desiredNode.ChildNodes[i].LocalName == "PartyId")
                                {
                                    string partyId = desiredNode.ChildNodes[i].InnerText;
                                    customerComplete.PartyId = Convert.ToInt64(partyId);

                                    break;
                                }
                            }
                            response.Close();

                        }

                    }

                }
                return customerComplete;
            }


            catch (Exception ex)
            {
                MessageBox.Show("OrgNOCreado" + ex.InnerException.ToString());
                return customerComplete;
            }
        }
        private CustomerComplete CallCreateCustomerAccount(CustomerComplete customerComplete)
        {

            try
            {
                GlobalContext.LogMessage("Crear CustoemerAccount");
                string envelope = "<soapenv:Envelope" +
                    "   xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"" +
                    "   xmlns:typ=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/customerAccountService/applicationModule/types/\"" +
                    "   xmlns:cus=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/customerAccountService/\"" +
                    "   xmlns:cus1=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/flex/custAccountContactRole/\"" +
                    "   xmlns:par=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/partyService/\"" +
                    "   xmlns:sour=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/flex/sourceSystemRef/\"" +
                    "   xmlns:cus2=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/flex/custAccountContact/\"" +
                    "   xmlns:cus3=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/flex/custAccountRel/\"" +
                    "   xmlns:cus4=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/flex/custAccountSiteUse/\"" +
                    "   xmlns:cus5=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/flex/custAccountSite/\"" +
                    "   xmlns:cus6=\"http://xmlns.oracle.com/apps/cdm/foundation/parties/flex/custAccount/\">" +
                    "<soapenv:Header/>" +
                    "<soapenv:Body>" +
                    "<typ:createCustomerAccount>" +
                    "<typ:customerAccount>" +
                    "<cus:CreatedByModule>HZ_WS</cus:CreatedByModule>" +
                    "<cus:PartyId>" + customerComplete.PartyId + "</cus:PartyId>" +
                    "<cus:AccountName>" + customerComplete.NombreOrg.ToUpper() + "</cus:AccountName>" +
                    "<cus:CustomerAccountSite>" +
                    "<cus:PartySiteId>" + customerComplete.PartySiteId + "</cus:PartySiteId>" +
                    "<cus:SetId>300000000001839</cus:SetId>" +
                    "<cus:CreatedByModule>HZ_WS</cus:CreatedByModule>" +
                    "<cus:CustomerAccountSiteUse>" +
                    "<cus:SiteUseCode>BILL_TO</cus:SiteUseCode>" +
                    "<cus:CreatedByModule>HZ_WS</cus:CreatedByModule>" +
                    "</cus:CustomerAccountSiteUse>" +
                    "<cus:CustomerAccountSiteUse>" +
                    "<cus:SiteUseCode>SHIP_TO</cus:SiteUseCode>" +
                    "<cus:CreatedByModule>HZ_WS</cus:CreatedByModule>" +
                    "</cus:CustomerAccountSiteUse>" +
                    "</cus:CustomerAccountSite>" +
                    "</typ:customerAccount>" +
                    "</typ:createCustomerAccount>" +
                    "</soapenv:Body>" +
                "</soapenv:Envelope>";
                byte[] byteArray = Encoding.UTF8.GetBytes(envelope);
                // Construct the base 64 encoded string used as credentials for the service call
                byte[] toEncodeAsBytes = System.Text.ASCIIEncoding.ASCII.GetBytes("itotal" + ":" + "Oracle123");
                string credentials = System.Convert.ToBase64String(toEncodeAsBytes);
                // Create HttpWebRequest connection to the service
                HttpWebRequest request =
                 (HttpWebRequest)WebRequest.Create("https://egqy-test.fa.us6.oraclecloud.com:443/crmService/CustomerAccountService");
                // Configure the request content type to be xml, HTTP method to be POST, and set the content length
                request.Method = "POST";
                request.ContentType = "text/xml;charset=UTF-8";
                request.ContentLength = byteArray.Length;
                // Configure the request to use basic authentication, with base64 encoded user name and password, to invoke the service.
                request.Headers.Add("Authorization", "Basic " + credentials);
                // Set the SOAP action to be invoked; while the call works without this, the value is expected to be set based as per standards
                request.Headers.Add("SOAPAction", "http://xmlns.oracle.com/apps/cdm/foundation/parties/customerAccountService/applicationModule/createCustomerAccount");
                // Write the xml payload to the request
                Stream dataStream = request.GetRequestStream();
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();

                // Write the xml payload to the request
                XDocument doc;
                XmlDocument docu = new XmlDocument();
                string result;
                // Get the response and process it; In this example, we simply print out the response XDocument doc;
                using (WebResponse response = request.GetResponse())
                {

                    using (Stream stream = response.GetResponseStream())
                    {
                        doc = XDocument.Load(stream);
                        result = doc.ToString();
                        XmlDocument xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(result);
                        XmlNamespaceManager nms = new XmlNamespaceManager(xmlDoc.NameTable);
                        nms.AddNamespace("env", "http://schemas.xmlsoap.org/soap/envelope/");
                        nms.AddNamespace("wsa", "http://www.w3.org/2005/08/addressing");
                        nms.AddNamespace("ns2", "http://xmlns.oracle.com/apps/cdm/foundation/parties/customerAccountService/");
                        nms.AddNamespace("ns1", "http://xmlns.oracle.com/adf/svc/types/");
                        XmlNode desiredNode = xmlDoc.SelectSingleNode("//ns2:Value", nms);
                        if (desiredNode.HasChildNodes)
                        {

                            for (int i = 0; i < desiredNode.ChildNodes.Count; i++)
                            {
                                if (desiredNode.ChildNodes[i].LocalName == "CustomerAccountId")
                                {
                                    string customeraccountId = desiredNode.ChildNodes[i].InnerText;
                                    customerComplete.CustomerAccountId = Convert.ToInt64(customeraccountId);
                                }
                                if (desiredNode.ChildNodes[i].LocalName == "AccountNumber")
                                {
                                    string accountNumber = desiredNode.ChildNodes[i].InnerText;
                                    customerComplete.AccountNumber = Convert.ToInt32(accountNumber);
                                    break;
                                }

                            }

                        }

                    }
                    response.Close();
                }
                return customerComplete;
            }
            catch (Exception ex)
            {
                MessageBox.Show("CustomerAccNOcreado" + ex);

                return customerComplete;
            }

        }
        private CustomerComplete CallCreateCustomerProfile(CustomerComplete customerComplete)
        {
            try
            {
                GlobalContext.LogMessage("Crear CustomerProfile");
                string dateTime = DateTime.Now.ToString();
                string createddate = Convert.ToDateTime(dateTime).ToString("yyyy-MM-dd");

                string envelope = "<soapenv:Envelope" +
                   "   xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"" +
                   "   xmlns:typ=\"http://xmlns.oracle.com/apps/financials/receivables/customers/customerProfileService/types/\"" +
                   "   xmlns:cus=\"http://xmlns.oracle.com/apps/financials/receivables/customers/customerProfileService/\"" +
                   "   xmlns:cus1=\"http://xmlns.oracle.com/apps/financials/receivables/customerSetup/customerProfiles/model/flex/CustomerProfileDff/\"" +
                   "   xmlns:cus2=\"http://xmlns.oracle.com/apps/financials/receivables/customerSetup/customerProfiles/model/flex/CustomerProfileGdf/\">" +
                   "<soapenv:Header/>" +
                   "<soapenv:Body>" +
                   "<typ:createCustomerProfile>" +
                   "<typ:customerProfile>" +
                   "<cus:AccountNumber>" + customerComplete.AccountNumber + "</cus:AccountNumber>" +
                   "<cus:ProfileClassName>DEFAULT</cus:ProfileClassName>" +
                   "<cus:EffectiveEndDate>4712-12-01</cus:EffectiveEndDate>" +
                   "<cus:EffectiveStartDate>" + createddate + "</cus:EffectiveStartDate>" +
                   "<cus:PartyId>" + customerComplete.PartyId + "</cus:PartyId>" +
                   "<cus:CollectorName>Default Collector</cus:CollectorName>" +
                   "</typ:customerProfile>" +
                   "</typ:createCustomerProfile>" +
                   "</soapenv:Body>" +
                   "</soapenv:Envelope>";
                byte[] byteArray = Encoding.UTF8.GetBytes(envelope);
                // Construct the base 64 encoded string used as credentials for the service call
                byte[] toEncodeAsBytes = System.Text.ASCIIEncoding.ASCII.GetBytes("itotal" + ":" + "Oracle123");
                string credentials = System.Convert.ToBase64String(toEncodeAsBytes);
                // Create HttpWebRequest connection to the service
                HttpWebRequest request =
                 (HttpWebRequest)WebRequest.Create("https://egqy-test.fa.us6.oraclecloud.com:443/fscmService/ReceivablesCustomerProfileService");
                // Configure the request content type to be xml, HTTP method to be POST, and set the content length
                request.Method = "POST";
                request.ContentType = "text/xml;charset=UTF-8";
                request.ContentLength = byteArray.Length;
                // Configure the request to use basic authentication, with base64 encoded user name and password, to invoke the service.
                request.Headers.Add("Authorization", "Basic " + credentials);
                // Set the SOAP action to be invoked; while the call works without this, the value is expected to be set based as per standards
                request.Headers.Add("SOAPAction", "http://xmlns.oracle.com/apps/financials/receivables/customers/customerProfileService/createCustomerProfile");
                // Write the xml payload to the request
                Stream dataStream = request.GetRequestStream();
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();

                // Write the xml payload to the request
                XDocument doc;
                XmlDocument docu = new XmlDocument();
                string result;
                // Get the response and process it; In this example, we simply print out the response XDocument doc;
                using (WebResponse response = request.GetResponse())
                {
                    using (Stream stream = response.GetResponseStream())
                    {
                        doc = XDocument.Load(stream);
                        result = doc.ToString();
                        XmlDocument xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(result);
                        XmlNamespaceManager nms = new XmlNamespaceManager(xmlDoc.NameTable);
                        nms.AddNamespace("env", "http://schemas.xmlsoap.org/soap/envelope/");
                        nms.AddNamespace("wsa", "http://www.w3.org/2005/08/addressing");
                        nms.AddNamespace("ns2", "http://xmlns.oracle.com/apps/financials/receivables/customerSetup/customerProfiles/model/flex/CustomerProfileGdf/");
                        nms.AddNamespace("ns3", "http://xmlns.oracle.com/apps/financials/receivables/customers/customerProfileService/");
                        nms.AddNamespace("ns1", "http://xmlns.oracle.com/apps/financials/receivables/customerSetup/customerProfiles/model/flex/CustomerProfileDff/");
                        XmlNode desiredNode = xmlDoc.SelectSingleNode("//ns3:Value", nms);
                        if (desiredNode.HasChildNodes)
                        {
                            if (desiredNode.ChildNodes.Count > 0)
                            {
                                customerComplete.CustomerProfileCreated = true;
                            }

                        }

                    }
                    response.Close();
                }
                return customerComplete;
            }
            catch (Exception ex)
            {
                MessageBox.Show("CustomerProfileNOCreado" + ex.Message);
                return customerComplete;
            }
        }
        public string GetName(int FieldID)
        {
            //Obtiene el nombre del campo
            string result = "Name";
            IList<IOptlistItem> CustomFieldOptList = GlobalContext.GetOptlist(92);
            foreach (IOptlistItem CustomField in CustomFieldOptList)
            {
                if (FieldID == (int)CustomField.ID)
                {
                    result = CustomField.Label;
                }
            }

            return result;
        }
        public string GetValue(int FieldID, int FieldValue)
        {
            string result = "";
            //Obtiene el valor de un campo tipo menu
            int customFieldOptListID = FieldID + (0x3 << 24);
            IList<IOptlistItem> CustomFieldLabelOptList = GlobalContext.GetOptlist(customFieldOptListID);
            foreach (IOptlistItem CustomFieldLabel in CustomFieldLabelOptList)
            {
                if (CustomFieldLabel.ID == FieldValue)
                {
                    result = CustomFieldLabel.Label;
                }
            }
            return result;
        }
        public string GetDataType(int DataType)
        {
            string datatype = "";
            switch (DataType)
            {
                case 1:
                    datatype = "Menu";
                    break;
                case 2:
                    datatype = "YesNo";
                    break;
                case 3:
                    datatype = "";
                    break;
                case 4:
                    datatype = "";
                    break;
                case 5:
                    datatype = "String";
                    break;
                case 6:
                    datatype = "";
                    break;
                case 7:
                    datatype = "";
                    break;
                case 8:
                    datatype = "";
                    break;
            }
            return datatype;
        }
        public bool Init()
        {
            try
            {
                bool result = false;
                EndpointAddress endPointAddr = new EndpointAddress(GlobalContext.GetInterfaceServiceUrl(ConnectServiceType.Soap));
                // Minimum required
                BasicHttpBinding binding = new BasicHttpBinding(BasicHttpSecurityMode.TransportWithMessageCredential);
                binding.Security.Message.ClientCredentialType = BasicHttpMessageCredentialType.UserName;
                binding.ReceiveTimeout = new TimeSpan(0, 10, 0);
                binding.MaxReceivedMessageSize = 1048576; //1MB
                binding.SendTimeout = new TimeSpan(0, 10, 0);
                // Create client proxy class
                clientRN = new RightNowSyncPortClient(binding, endPointAddr);
                // Ask the client to not send the timestamp
                BindingElementCollection elements = clientRN.Endpoint.Binding.CreateBindingElements();
                elements.Find<SecurityBindingElement>().IncludeTimestamp = false;
                clientRN.Endpoint.Binding = new CustomBinding(elements);
                // Ask the Add-In framework the handle the session logic
                GlobalContext.PrepareConnectSession(clientRN.ChannelFactory);
                if (clientRN != null)
                {
                    result = true;
                }

                return result;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }
        public string getIsoCode(int Country)
        {
            string iSO = "";
            ClientInfoHeader clientInfoHeader = new ClientInfoHeader();
            APIAccessRequestHeader aPIAccessRequest = new APIAccessRequestHeader();
            clientInfoHeader.AppID = "Query Example";
            String queryString = "SELECT ISOCode FROM Country  WHERE ID = " + Country;
            clientRN.QueryCSV(clientInfoHeader, aPIAccessRequest, queryString, 10000, "|", false, false, out CSVTableSet queryCSV, out byte[] FileData);
            foreach (CSVTable table in queryCSV.CSVTables)
            {
                String[] rowData = table.Rows;
                foreach (String data in rowData)
                {
                    iSO = data;
                }
            }

            return iSO;


        }
        public string getEstado(int Country, int Province)
        {
            string Estado = "";
            ClientInfoHeader clientInfoHeader = new ClientInfoHeader();
            APIAccessRequestHeader aPIAccessRequest = new APIAccessRequestHeader();
            clientInfoHeader.AppID = "Query Example";
            String queryString = "SELECT Country.provinces.name FROM Country WHERE Country.ID =" + Country + " AND Country.Provinces.ID=" + Province + "";
            clientRN.QueryCSV(clientInfoHeader, aPIAccessRequest, queryString, 10000, "|", false, false, out CSVTableSet queryCSV, out byte[] FileData);
            foreach (CSVTable table in queryCSV.CSVTables)
            {
                String[] rowData = table.Rows;
                foreach (String data in rowData)
                {
                    Estado = data;
                }
            }

            return Estado;


        }

    }
    
    [AddIn("Workspace Ribbon Button AddIn", Version = "1.0.0.0")]
    public class WorkspaceRibbonButtonFactory : IWorkspaceRibbonButtonFactory
    {
        private IGlobalContext GlobalContext;

        public IWorkspaceRibbonButton CreateControl(bool inDesignMode, IRecordContext RecordContext)
        {
            return new WorkspaceRibbonAddIn(inDesignMode, RecordContext, GlobalContext);
        }

        public System.Drawing.Image Image32
        {
            get { return Properties.Resources.Cloud32; }
        }

        public System.Drawing.Image Image16
        {
            get { return Properties.Resources.Cloud16; }
        }

        public string Text
        {
            get { return "Creación de Cuenta"; }
        }

        public string Tooltip
        {
            get { return "Creación de Cuenta"; }
        }

        public bool Initialize(IGlobalContext GlobalContext)
        {
            this.GlobalContext = GlobalContext;
            return true;
        }

    }
}


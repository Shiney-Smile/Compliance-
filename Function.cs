using Amazon.Lambda.Core;
using Aras.IOM;
using Aras.IOME;
using PMI_CLIPP_CHG_OBJ_COMPLIANCE_LIB.CommonDataModel;
using PMI_CLIPP_CHG_OBJ_COMPLIANCE_LIB.ConnectionManager;
using PMI_CLIPP_CHG_OBJ_COMPLIANCE_LIB.CustomException;
using PMI_CLIPP_CHG_OBJ_COMPLIANCE_LIB.ItemType;
using PMI_CLIPP_CHG_OBJ_COMPLIANCE_LIB.ItemType.ChangeObject;
using PMI_CLIPP_CHG_OBJ_COMPLIANCE_LIB.RequestResponseModel;
using PMI_CLIPP_CHG_OBJ_COMPLIANCE_LIB.Util;
using PMI_CLIPP_TEST_APPLICATION;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace PMI_CLIPP_PART_COMPLIANCE_CHECK
{
    public class Function
    {
        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input">The event for the Lambda function handler to process.</param>
        /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
        /// <returns></returns>
        public ComplianceCheckRequestResponse FunctionHandler(ComplianceCheckRequestResponse request, ILambdaContext context)
        {
            Console.WriteLine("Step 0 : PMI_CLIPP_PART_COMPLIANCE_CHECK : Program Started.");
            Innovator innovator = null;
            try
            {
                if (string.IsNullOrEmpty(request.partNumber)) throw new InvalidPartNumberException();
                if (string.IsNullOrEmpty(request.partId)) throw new InvalidPartIDException();
                if (string.IsNullOrEmpty(request.changeType)) throw new InvalidChangeTypeException();
                if (string.IsNullOrEmpty(request.username)) throw new InvalidUsernameException();

                innovator = InnovatorConnectionManager.GetNewInnovatorConnection();
                Console.WriteLine("Step 1 : PMI_CLIPP_PART_COMPLIANCE_CHECK : Aras Connection Succussfully.");

                List<string> propertyList = new List<string> { "pmi_compliance_status", "pmi_child_compliance_status", "pmi_business_compliance" };

                Item PartItem = innovator.getItemById(ItemTypeName.Part, request.partId);
                ProductVariant pv = new ProductVariant(innovator);
                if (request.partNumber == null)
                {
                    Console.WriteLine("Step 2 : PMI_CLIPP_PART_COMPLIANCE_CHECK : part number is null.");
                    throw new ArgumentNullException("ProductVariant::FetchPV() : part number is null.");
                }
                Console.WriteLine("Step 2 : PMI_CLIPP_PART_COMPLIANCE_CHECK : part number is : " + request.partNumber.ToString());

                //bool checkType = pv.CheckApplicabilityOfPart(request.partNumber, request.changeType);
                bool checkType = pv.CheckApplicabilityOfPart(PartItem, request.changeType);
                Console.WriteLine("Step 3 : PMI_CLIPP_PART_COMPLIANCE_CHECK : checkType : " + checkType.ToString());

                if (checkType == true)
                {
                    string propertyCheck = "pmi_compliance_status";
                    request.isPVApplicable = true;
                    request.isPartApplicable = true;

                    if (!String.IsNullOrEmpty(request.partId))
                    {
                        if (!propertyList.Contains(propertyCheck))
                        {
                            throw new Exception(propertyCheck + " is not a valid property");
                        }

                        checkComplianceECO cc = new checkComplianceECO(innovator);
                        var (status, complianceStatusMsg, businessLmtFlag, complianceFlag) = cc.checkComplianceOfBoMModel(propertyCheck, request.partId, request.pvId, request.changeType);

                        if (status == "NOT COMPLIANT")
                        {
                            request.complianceResult = ComplianceResult.NonCompliant;
                        }
                        else if (status == "OTHER LIMITATIONS")
                        {
                            request.complianceResult = ComplianceResult.OtherLimitaton;
                        }
                        else //Compliant
                        {
                            request.complianceResult = ComplianceResult.Compliant;
                        }
                        request.complianceInputflag = complianceFlag;
                        request.businessLimitationflag = businessLmtFlag;
                        request.statusMessage = complianceStatusMsg;
                        request.affectedItemCount = request.affectedItemCount;
                    }
                    else
                    {
                        Console.WriteLine("Step 4 : PMI_CLIPP_PART_COMPLIANCE_CHECK : Property Name cannot be empty.");
                        throw new Exception("Property Name cannot be empty");
                    }
                }
                else
                {
                    request.complianceResult = ComplianceResult.Compliant;
                    request.isPVApplicable = false;
                    request.isPartApplicable = false;
                    request.affectedItemCount = 0;
                    //request.Error = ErrorInformation.ErrorInfoForPartApplicability();
                }
                Console.WriteLine("Step 5 : PMI_CLIPP_PART_COMPLIANCE_CHECK : request.complianceResult : " + request.complianceResult.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Step 6 : PMI_CLIPP_PART_COMPLIANCE_CHECK : Exception Error occured : " + ex.Message);
                request.Error = ErrorInformation.ConvertExceptionToErrorInformation(ex);
            }
            finally
            {
                if (innovator != null)
                    InnovatorConnectionManager.Logout(innovator);
            }
            Console.WriteLine("Step 7 : PMI_CLIPP_PART_COMPLIANCE_CHECK : Program Ended.");

            return request;
        }
    }
}
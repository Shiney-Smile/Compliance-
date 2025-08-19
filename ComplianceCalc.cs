using System;
using Aras.IOM;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CLIPP_SDK
{
    public class ComplianceCalc
    {
        public static Item pmi_CreateCompCalculationOutput(Innovator innovator, Item item, Aras.Server.Core.CallContext CCO)
        {
            Innovator inn = item.getInnovator();
            string relTypeOfItem = "";
            string relTypeOfCompCalOutput = "";

            if (item.getType() == "pmi_ComplianceReplicaBOM")
            {
                relTypeOfItem = "pmi_regSpec_CompPartBoM";
                relTypeOfCompCalOutput = "pmi_RepBomRegSpecCompStatus";
            }
            else
            {
                relTypeOfItem = "pmi_Regulatory_Compliance";
                relTypeOfCompCalOutput = "pmi_RegSpecComplianceStatus";
            }

            Item getRegSpecs = inn.newItem(relTypeOfItem, "get");
            getRegSpecs.setProperty("source_id", item.getProperty("id", ""));
            getRegSpecs.setAttribute("select", "related_id(id,keyed_name)");
            getRegSpecs = getRegSpecs.apply();
            int getRegSpecsCount = getRegSpecs.getItemCount();
            if (getRegSpecs.isError() || getRegSpecs.isEmpty())
            {
                throw new Exception("Compliance Calculations tab is EMPTY.Please add Reg. Specifications and then proceed");
            }
            Item getCompCalOutputItem = null;
            getCompCalOutputItem = inn.newItem("pmi_ComplianceCalOutput", "get");
            if (item.getType() == "pmi_ComplianceReplicaBOM")
                getCompCalOutputItem.setProperty("pmi_replicatedbom_part_code", item.getProperty("keyed_name", ""));
            else
                getCompCalOutputItem.setProperty("pmi_part_code", item.getProperty("keyed_name", ""));
            getCompCalOutputItem.setAttribute("select", "id,keyed_name");
            getCompCalOutputItem = getCompCalOutputItem.apply();
            if (getCompCalOutputItem.isEmpty() || getCompCalOutputItem.isError())
            {
                getCompCalOutputItem = inn.newItem("pmi_ComplianceCalOutput", "add");
                if (item.getType() == "pmi_ComplianceReplicaBOM")
                {
                    getCompCalOutputItem.setProperty("pmi_replicatedbom_part_code", item.getProperty("keyed_name", ""));
                    getCompCalOutputItem.setProperty("pmi_replicatedbom_description", "Compliance Calculation Output for " + item.getProperty("keyed_name", ""));
                }
                else
                {
                    getCompCalOutputItem.setProperty("pmi_part_code", item.getProperty("keyed_name", ""));
                    getCompCalOutputItem.setProperty("pmi_description", "Compliance Calculation Output for " + item.getProperty("keyed_name", ""));
                }
                getCompCalOutputItem.setAttribute("doGetItem", "0");
                getCompCalOutputItem = getCompCalOutputItem.apply();
                if (getCompCalOutputItem.isError())
                {
                    return inn.newError("Error while creating Compliance Calculation Output.");
                }
            }
            Item appendItems = null;
            Item appendIngredientItems = null;
            int getSubsFromPartMDCount = 0;
            List<KeyValuePair<string, string>> regSpecs = new List<KeyValuePair<string, string>>();
            List<KeyValuePair<string, string>> regParameters = new List<KeyValuePair<string, string>>();
            List<string> subsFromMD = new List<string>();
            List<string> ingredientSubsFromMD = new List<string>();
            Dictionary<string, string> substDict = new Dictionary<string, string>();
            Dictionary<string, string> ingredientSubDict = new Dictionary<string, string>();

            Item deleteSubstItems = inn.applySQL(string.Format(@"delete from innovator.{0} where SOURCE_ID = '{1}'", relTypeOfCompCalOutput, getCompCalOutputItem.getID()));
            if (deleteSubstItems.isError())
            {
                throw new InvalidOperationException("Error removing the Compliance Status. Please try again.");
            }

            if (item.getType() == "pmi_ComplianceReplicaBOM")
            {
                Item getIdofDataModelItem = innovator.newItem("pmi_TreeGridBOMDataModel", "get");
                getIdofDataModelItem.setProperty("pmi_item_code", item.getProperty("ReplicatedBomPartCode", ""));
                getIdofDataModelItem.setProperty("pmi_container", item.getProperty("id", ""));
                getIdofDataModelItem.setProperty("pmi_type", "Part");
                getIdofDataModelItem.setAttribute("select", "id");
                getIdofDataModelItem = getIdofDataModelItem.apply();
                if (!getIdofDataModelItem.isError() && !getIdofDataModelItem.isEmpty() && getIdofDataModelItem.getItemCount() > 0)
                {
                    Item result = innovator.newItem("SQL", "SQL PROCESS");
                    result.setProperty("name", "pmi_PCO_GetAllTreeGrid_MaterialDisclosure");
                    result.setProperty("PROCESS", "CALL");
                    result.setProperty("ARG1", getIdofDataModelItem.getID());
                    result = result.apply();

                    int resultCount = result.getItemCount();
                    if (!result.isError() && !result.isEmpty() && resultCount > 0 && result.getResult() != "")
                    {
                        for (int r = 0; r < resultCount; r++)
                        {
                            Item itemByIndex = result.getItemByIndex(r);
                            string relatedId = itemByIndex.getProperty("related_id");
                            Item getSubsFromMD = inn.newItem("pmi_TreeGridRelsStructure", "get");
                            getSubsFromMD.setProperty("source_id", relatedId);
                            getSubsFromMD.setAttribute("select", "id,keyed_name,related_id(pmi_ppm,pmi_sub_code,pmi_ingredientgroup)");
                            getSubsFromMD = getSubsFromMD.apply();
                            if (getSubsFromMD.isError() || getSubsFromMD.isEmpty() || getSubsFromMD.getItemCount() <= 0)
                            {
                                throw new Exception("No Substances are found in the Material Disclosure: " + item.getProperty("MaterialDiscCode", ""));
                            }
                            getSubsFromPartMDCount = getSubsFromMD.getItemCount();

                            for (int i = 0; i < getSubsFromPartMDCount; i++)
                            {
                                Item getItemByIndex = getSubsFromMD.getItemByIndex(i);
                                if (substDict.ContainsKey(getItemByIndex.getRelatedItem().getProperty("pmi_sub_code", "")))
                                {
                                    string previousValue = substDict[getItemByIndex.getRelatedItem().getProperty("pmi_sub_code", "")];
                                    string newValue = getItemByIndex.getRelatedItem().getProperty("pmi_ppm", "0");
                                    float total = float.Parse(previousValue) + float.Parse(newValue);
                                    substDict[getItemByIndex.getRelatedItem().getProperty("pmi_sub_code", "")] = total.ToString();
                                }
                                else
                                {
                                    substDict.Add(getItemByIndex.getRelatedItem().getProperty("pmi_sub_code", ""), getItemByIndex.getRelatedItem().getProperty("pmi_ppm", "0"));
                                }
                                if (ingredientSubDict.ContainsKey(getItemByIndex.getRelatedItem().getProperty("pmi_ingredientgroup", "")))
                                {
                                    string previousValue = ingredientSubDict[getItemByIndex.getRelatedItem().getProperty("pmi_ingredientgroup", "")];
                                    string newValue = getItemByIndex.getRelatedItem().getProperty("pmi_ppm", "0");
                                    float total = float.Parse(previousValue) + float.Parse(newValue);
                                    ingredientSubDict[getItemByIndex.getRelatedItem().getProperty("pmi_ingredientgroup", "")] = total.ToString();
                                }
                                else
                                {
                                    ingredientSubDict.Add(getItemByIndex.getRelatedItem().getProperty("pmi_ingredientgroup", ""), getItemByIndex.getRelatedItem().getProperty("pmi_ppm", "0"));
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new Exception("No Material Disclosure found for current Part!");
                    }
                }
            }
            else
            {
                Item partResult = inn.applySQL(string.Format(@"select top 1 md.id, md.PMI_ITEMCODE from innovator.PMI_MATERIALDISCLOSURE md WITH(NOLOCK) join innovator.pmi_MDPartGroup mdPartGrp WITH(NOLOCK) on md.id=mdPartGrp.SOURCE_ID join innovator.pmi_PartGroup_Part_Rel prtGrpRel WITH(NOLOCK) on prtGrpRel.SOURCE_ID=mdPartGrp.RELATED_ID join innovator.part part WITH(NOLOCK) on part.id=prtGrpRel.RELATED_ID where part.ID='{0}' and md.IS_CURRENT=1 union select top 1 md.id, md.PMI_ITEMCODE from innovator.PMI_MATERIALDISCLOSURE md WITH(NOLOCK) join innovator.pmi_MaterialDisclosurePart mdPart WITH(NOLOCK) on md.id=mdPart.SOURCE_ID join innovator.part part WITH(NOLOCK) on part.id=mdPart.RELATED_ID where part.ID='{0}' and md.IS_CURRENT=1", item.getProperty("id", "")));
                int partResultCount = partResult.getItemCount();
                if (!partResult.isError() && !partResult.isEmpty() && partResultCount > 0 && partResult.getResult() != "")
                {
                    Item getSubsFromPartMD = inn.newItem("pmi_MaterialDisclr_Substance", "get");
                    getSubsFromPartMD.setProperty("source_id", partResult.getProperty("id", ""));
                    getSubsFromPartMD.setAttribute("select", "related_id,id,keyed_name,pmi_disclosed_ppm");
                    getSubsFromPartMD = getSubsFromPartMD.apply();
                    if (getSubsFromPartMD.isError() || getSubsFromPartMD.isEmpty() || getSubsFromPartMD.getItemCount() <= 0)
                    {
                        throw new Exception("No Substances are found in the Material Disclosure: " + item.getProperty("MaterialDiscCode", ""));
                    }
                    getSubsFromPartMDCount = getSubsFromPartMD.getItemCount();

                    for (int i = 0; i < getSubsFromPartMDCount; i++)
                    {
                        Item getItemByIndex = getSubsFromPartMD.getItemByIndex(i);
                        substDict.Add(getItemByIndex.getProperty("related_id", ""), getItemByIndex.getProperty("pmi_disclosed_ppm", "0"));
                    }
                }
                else
                {
                    throw new Exception("No Material Disclosure found for current Part!");
                }
            }
            for (int i = 0; i < getRegSpecsCount; i++)
            {
                Item getItemByIndex = getRegSpecs.getItemByIndex(i);
                Item getSubstances = inn.newItem("pmi_RegulatorySpec_Substance", "get");
                getSubstances.setProperty("source_id", getItemByIndex.getProperty("related_id", ""));
                getSubstances.setAttribute("select", "id,related_id(pmi_ingredientgroup,pmi_casnumber),source_id,keyed_name,pmi_threshold");
                getSubstances = getSubstances.apply();

                if (getSubstances.isError() || getSubstances.isEmpty() || getSubstances.getItemCount() <= 0)
                {
                    throw new Exception("No Substances found for Regulatory Specifications: " + getItemByIndex.getRelatedItem().getProperty("keyed_name", ""));
                }
                Item addStatus = null;
                int getSubstancesCount = getSubstances.getItemCount();

                for (int j = 0; j < getSubstancesCount; j++)
                {
                    Item getSubByIndex = getSubstances.getItemByIndex(j);
                    Item relatedItem = getSubByIndex.getRelatedItem();
                    regSpecs.Add(new KeyValuePair<string, string>(relatedItem.getProperty("id", ""), getSubByIndex.getPropertyAttribute("source_id", "keyed_name")));
                    string thresholdRange = string.Empty;

                    string getCASNUmber = relatedItem.getProperty("pmi_casnumber", "");
                    string getIngredientGroup = relatedItem.getProperty("pmi_ingredientgroup", "");
                    if (substDict.TryGetValue(getSubByIndex.getProperty("related_id", ""), out thresholdRange))
                    {
                        addStatus = inn.newItem(relTypeOfCompCalOutput, "add");
                        addStatus.setProperty("pmi_reg_spec_code", getItemByIndex.getPropertyAttribute("related_id", "keyed_name"));
                        addStatus.setProperty("pmi_substance_code", getSubByIndex.getPropertyAttribute("related_id", "keyed_name"));
                        addStatus.setProperty("source_id", getCompCalOutputItem.getProperty("id", ""));
                        addStatus.setProperty("pmi_threshold_from_rs", getSubByIndex.getProperty("pmi_threshold", ""));
                        addStatus.setProperty("pmi_casnumber", getCASNUmber);
                        addStatus.setProperty("pmi_ingredientgroup", getIngredientGroup);
                        addStatus.setProperty("pmi_status", "");
                        addStatus.setAttribute("doGetItem", "0");
                        if (!String.IsNullOrEmpty(thresholdRange))
                        {
                            addStatus.setProperty("pmi_threshold_ppm", substDict[getSubByIndex.getProperty("related_id", "")]);
                        }
                    }
                    else
                    {
                        continue;
                    }
                    if (appendItems == null)
                    {
                        appendItems = addStatus;
                    }
                    else
                    {
                        appendItems.appendItem(addStatus);
                    }
                }
                List<string> keysOfRegSpecs = (from keys in regSpecs select keys.Key).ToList();
                subsFromMD = substDict.Keys.ToList();
                List<string> missingSub = subsFromMD.Except(keysOfRegSpecs).ToList();
                foreach (string item1 in subsFromMD)
                {
                    if (missingSub.Contains(item1))
                    {
                        Item getSub = inn.newItem("pmi_RegulatorySpec_Substance", "get");
                        getSub.setProperty("related_id", item1);
                        getSub.setProperty("source_id", getItemByIndex.getProperty("related_id", ""));
                        getSub.setAttribute("select", "related_id(id,keyed_name,pmi_casnumber,pmi_ingredientgroup)");
                        getSub = getSub.apply();
                        string subCode = "";
                        string casNumber = "";
                        string ingredientGroup = "";
                        if (getSub.isError() || getSub.isEmpty() || getSub.getItemCount() <= 0)
                        {
                            Item fetchSub = inn.newItem("pmi_Substance", "get");
                            fetchSub.setAttribute("select", "id,keyed_name,pmi_casnumber,pmi_ingredientgroup");
                            fetchSub.setProperty("id", item1);
                            fetchSub = fetchSub.apply();
                            if (fetchSub.isError())
                            {
                                throw new Exception("No Substances found for Regulatory Specifications");
                            }
                            subCode = fetchSub.getProperty("keyed_name", "");
                            casNumber = fetchSub.getProperty("pmi_casnumber", "");
                            ingredientGroup = fetchSub.getProperty("pmi_ingredientgroup", "");
                        }
                        addStatus = inn.newItem(relTypeOfCompCalOutput, "add");
                        addStatus.setProperty("pmi_reg_spec_code", getItemByIndex.getPropertyAttribute("related_id", "keyed_name"));
                        addStatus.setProperty("pmi_substance_code", subCode);
                        addStatus.setProperty("pmi_threshold_from_rs", "");
                        addStatus.setProperty("pmi_casnumber", casNumber);
                        addStatus.setProperty("pmi_ingredientgroup", ingredientGroup);
                        addStatus.setProperty("pmi_status", "Substance Not Present in Reg Specs");
                        addStatus.setProperty("source_id", getCompCalOutputItem.getProperty("id", ""));
                        //addStatus = addStatus.apply();
                        if (subsFromMD.Contains(item1))
                        {
                            addStatus.setProperty("pmi_threshold_ppm", substDict[item1]);
                        }
                        if (appendItems == null)
                        {
                            appendItems = addStatus;
                        }
                        else
                        {
                            appendItems.appendItem(addStatus);
                        }
                    }
                }
                keysOfRegSpecs.Clear();
                regSpecs.Clear();

				if (item.getType() == "pmi_ComplianceReplicaBOM")
				{
					Item getParValues = inn.newItem("pmi_RegulatorySpec_GHP", "get");
					getParValues.setProperty("source_id", getItemByIndex.getProperty("related_id", ""));
					getParValues.setAttribute("select", "id,related_id(pmi_description),source_id,keyed_name,pmi_max_value");
					getParValues = getParValues.apply();

					if (getParValues.isError() || getParValues.isEmpty() || getParValues.getItemCount() <= 0)
					{
						throw new Exception("No Parameters found for Regulatory Specifications: " + getItemByIndex.getRelatedItem().getProperty("keyed_name", ""));
					}

					Item deleteingredientSubstItems = inn.applySQL(string.Format(@"delete from innovator.pmi_RepBomIngredientGrouping where SOURCE_ID = '{0}'", getCompCalOutputItem.getID()));
					
					Item addParameters = null;
					int getParValuesCount = getParValues.getItemCount();

					for (int j = 0; j < getParValuesCount; j++)
					{
						Item getParByIndex = getParValues.getItemByIndex(j);
						Item relatedItem = getParByIndex.getRelatedItem();
						
						if (ingredientSubDict.ContainsKey(relatedItem.getProperty("pmi_description", "")))
						{
							regParameters.Add(new KeyValuePair<string, string>(relatedItem.getProperty("pmi_description", ""), getParByIndex.getPropertyAttribute("source_id", "keyed_name")));

							string thresholdRange = string.Empty;

							if (ingredientSubDict.TryGetValue(relatedItem.getProperty("pmi_description", ""), out thresholdRange))
							{
								addParameters = inn.newItem("pmi_RepBomIngredientGrouping", "add");
								addParameters.setProperty("pmi_reg_spec_code", getItemByIndex.getPropertyAttribute("related_id", "keyed_name"));
								addParameters.setProperty("pmi_ingredientgroup", relatedItem.getProperty("pmi_description", ""));
								addParameters.setProperty("source_id", getCompCalOutputItem.getProperty("id", ""));
								addParameters.setProperty("pmi_rs_par_max_value", getParByIndex.getProperty("pmi_max_value", ""));
								addParameters.setAttribute("doGetItem", "0");
								if (!String.IsNullOrEmpty(thresholdRange))
								{
									addParameters.setProperty("pmi_roll_up_sub_ingredient_ppm", ingredientSubDict[relatedItem.getProperty("pmi_description", "")]);
								}
							}
						}
						else
						{
							continue;
						}
						if (appendIngredientItems == null)
						{
							appendIngredientItems = addParameters;
						}
						else
						{
							appendIngredientItems.appendItem(addParameters);
						}
					}
					List<string> keysOfRegParameters = (from keys in regParameters select keys.Key).ToList();
					ingredientSubsFromMD = ingredientSubDict.Keys.ToList();
					List<string> missingIngredientSub = ingredientSubsFromMD.Except(keysOfRegParameters).ToList();
					foreach (string item1 in ingredientSubsFromMD)
					{
						if (missingIngredientSub.Contains(item1))
						{
							addParameters = inn.newItem("pmi_RepBomIngredientGrouping", "add");
							addParameters.setProperty("pmi_reg_spec_code", getItemByIndex.getPropertyAttribute("related_id", "keyed_name"));
							addParameters.setProperty("pmi_ingredientgroup", item1);
							addParameters.setProperty("source_id", getCompCalOutputItem.getProperty("id", ""));
							addParameters.setAttribute("doGetItem", "0");
							if (ingredientSubsFromMD.Contains(item1))
							{
								addParameters.setProperty("pmi_roll_up_sub_ingredient_ppm", ingredientSubDict[item1]);
							}
							if (appendIngredientItems == null)
							{
								appendIngredientItems = addParameters;
							}
							else
							{
								appendIngredientItems.appendItem(addParameters);
							}
						}
					}
					keysOfRegParameters.Clear();
					regParameters.Clear();
				}
            }
            Item applyAML = null;
            Item applyIngGropingAML = null;
            try
            {
                if (appendItems.getItemCount() == 1)
                {
                    applyAML = inn.applyAML("<AML>" + appendItems.ToString() + "</AML>");
                }
                else
                {
                    applyAML = inn.applyAML(appendItems.ToString());
                }
				if (item.getType() == "pmi_ComplianceReplicaBOM")
				{
					if (appendIngredientItems.getItemCount() == 1)
					{
						applyIngGropingAML = inn.applyAML("<AML>" + appendIngredientItems.ToString() + "</AML>");
					}
					else
					{
						applyIngGropingAML = inn.applyAML(appendIngredientItems.ToString());
					}
				}
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            return item;
        }
    }
}
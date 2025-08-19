using Aras.IOM;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace CLIPP_SDK
{
    public class Workflow
    {
        public static Item pmi_GetWorkflowItem(Innovator innovator, Item item, Aras.Server.Core.CallContext CCO)
        {
            string activityId = item.getProperty("activity_id", string.Empty);
            string wflSelect = item.getProperty("wfl_select", string.Empty);

            Item wflItem = innovator.newItem("Workflow", "get");
            wflItem.setAttribute("serverEvents", "0");
            wflItem.setAttribute("select", wflSelect);
            Item wflProc = wflItem.createRelatedItem("Workflow Process", "get");
            wflProc.setAttribute("serverEvents", "0");
            wflProc.setAttribute("select", "name");
            Item wflProcAct = wflProc.createRelationship("Workflow Process Activity", "get");
            wflProcAct.setAttribute("serverEvents", "0");
            wflProcAct.setAttribute("select", "related_id");
            wflProcAct.setProperty("related_id", activityId);
            wflItem = wflItem.apply();           
            return wflItem;
        }

        public static void UpdateEffectivityDate(Item itm)
        {
            string effectivityDatestring = itm.getProperty("pmi_effectivity_date", "");
            string itemtypeName = itm.getType();
            string effectivity_date = DateTime.Parse(effectivityDatestring).ToString("M/d/yyyy");
            string current_date = DateTime.Now.ToString("M/d/yyyy");
            DateTime current_mfgeco_date = DateTime.Parse(effectivity_date);
            DateTime today_mfg_date = DateTime.Parse(current_date);
            if (today_mfg_date > current_mfgeco_date)
            {
                itm.getInnovator().applySQL("update innovator."+itemtypeName+" set pmi_effectivity_date=(SELECT [innovator].[ConvertFromLocal]('" + current_date + "',NULL)) where id ='" + itm.getID() + "'");
            }
        }
        public static void AddNewAssignment(Innovator innovator, string activityId, List<string> activityReviewers)
        {
            string votingWeight = (100 / activityReviewers.Count + 1).ToString();
            foreach (string activityReviewer in activityReviewers)
            {
                Item addAssignment = innovator.newItem("Activity Assignment", "add");
                addAssignment.setProperty("source_id", activityId);
                addAssignment.setProperty("related_id", activityReviewer);
                addAssignment.setProperty("voting_weight", votingWeight);
                addAssignment.setAttribute("doGetItem", "0");
                addAssignment = addAssignment.apply();
                if (addAssignment.isError())
                {
                    throw new InvalidOperationException("Error adding the activity assignments " + addAssignment.getErrorString());
                }
            }
        }
        public static void RemoveOldAssignments(Innovator innovator, string activityId)
        {
            string whereAttribute = String.Format(CultureInfo.InvariantCulture, "[activity_assignment].source_id = '{0}'", activityId);
            Item deleteAssignment = innovator.newItem("Activity Assignment", "delete");
            deleteAssignment.setAttribute("where", whereAttribute);
            deleteAssignment.setAttribute("doGetItem", "0");
            deleteAssignment = deleteAssignment.apply();
            if (deleteAssignment.isError())
            {
                throw new InvalidOperationException("Error removing the existing activity assignments. Please try again. " + deleteAssignment.getErrorString());
            }
        }
    }
}

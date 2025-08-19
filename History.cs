using Aras.IOM;
using System;
using System.Globalization;
using System.IO;
using System.Text;


namespace CLIPP_SDK
{
    public class History
    {
        public static Item pmi_CreateHistoryInformaton(Innovator innovator, Item item,Aras.Server.Core.CallContext CCO)
        {
            bool isContainerExists = true;
            string itemId = item.getProperty("itemId", "");
            string itemType = item.getProperty("itemType", "");
            string createHistoryContainer = item.getProperty("createHistoryContainer", "");
            if (String.IsNullOrEmpty(itemId) || String.IsNullOrEmpty(itemType))
            {
                return item;
            }
            Item getItem = GetItemToBeLogged(itemId, itemType, innovator);
            if (getItem.isError() || getItem.getItemCount() <= 0)
            {
                return item;
            }
            string configId = getItem.getProperty("config_id");
            if (configId.Length != 32)
            {
                return innovator.newError("ID passed is not valid. Please try again.");
            }
            string keyedName = getItem.getProperty("keyed_name", "");
            Item historyContainer = GetHistoryContainer(configId, innovator);
            if (string.IsNullOrEmpty(historyContainer.getResult()))
            {
                isContainerExists = false;
            }
            if (historyContainer.isError() || historyContainer.getItemCount() <= 0)
            {
                if (createHistoryContainer.Equals("true") && historyContainer.getErrorCode().Equals("0"))
                {
                    isContainerExists = false;
                }
                else
                {
                    return historyContainer;
                }
            }
            Aras.Server.Security.Identity histIdentity = Aras.Server.Security.Identity.GetByName("History Daemon");
            using (CCO.Permissions.GrantIdentity(histIdentity))
            {
                if (!isContainerExists)
                {
                    historyContainer = AddHistoryContainer(configId, keyedName, item);
                    if (historyContainer.isError())
                    {
                        return historyContainer;
                    }
                }
                Item newHistoryEntry = LogHistory(innovator, itemId, getItem, historyContainer, item);
                if (newHistoryEntry.isError())
                {
                    return newHistoryEntry;
                }
            }
            return item;
        }


        private static Item LogHistory(Innovator innovator, string itemId, Item getItem, Item historyContainer,Item item)
        {
            Item newHistoryEntry = innovator.newItem("History", "add");
            newHistoryEntry.setProperty("action", item.getProperty("historyAction", "No Action Associated"));
            newHistoryEntry.setProperty("comments", item.getProperty("historyComments", "No Comments Available"));
            newHistoryEntry.setProperty("item_id", itemId);
            newHistoryEntry.setProperty("sort_order", "0");
            newHistoryEntry.setProperty("source_id", historyContainer.getID());
            newHistoryEntry.setProperty("item_version", getItem.getProperty("generation", ""));
            newHistoryEntry.setProperty("item_major_rev", getItem.getProperty("major_rev", ""));
            newHistoryEntry.setProperty("item_state", getItem.getProperty("state", ""));
            newHistoryEntry.setProperty("created_on_tick", DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture));
            newHistoryEntry.setAttribute("doGetItem", "0");
            newHistoryEntry = newHistoryEntry.apply();
            return newHistoryEntry;
        }
        private static Item AddHistoryContainer(string configId, string keyedName, Item item)
        {
            Item historyContainer = item.newItem("History Container", "add");
            historyContainer.setProperty("item_config_id", configId);
            historyContainer.setProperty("keyed_name", keyedName);
            historyContainer.setAttribute("doGetItem", "0");
            historyContainer = historyContainer.apply();
            return historyContainer;
        }
        private static Item GetHistoryContainer(string configId, Innovator inn)
        {
            string sql = string.Format(CultureInfo.InvariantCulture, "SELECT TOP 1 ID FROM INNOVATOR.HISTORY_CONTAINER WHERE ITEM_CONFIG_ID = '{0}'", configId);
            return inn.applySQL(sql); ;
        }
        private static Item GetItemToBeLogged(string itemId, string itemType, Innovator innovator)
        {
            itemType = itemType.Replace(' ', '_');
            string sql = string.Format("select config_id,generation,major_rev,state,keyed_name from innovator.[{0}] where id='{1}'", itemType, itemId);
            Item getItem = innovator.applySQL(sql);
            return getItem;
        }
        }
}

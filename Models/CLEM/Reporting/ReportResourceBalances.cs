﻿using Models.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Models.Report;
using APSIM.Shared.Utilities;
using System.Data;
using System.IO;
using Models.CLEM.Resources;
using Models.Core.Attributes;

namespace Models.CLEM.Reporting
{
    /// <summary>
    /// A report class for writing output to the data store.
    /// </summary>
    [Serializable]
    [ViewName("UserInterface.Views.ReportView")]
    [PresenterName("UserInterface.Presenters.ReportPresenter")]
    [ValidParent(ParentType = typeof(ZoneCLEM))]
    [ValidParent(ParentType = typeof(CLEMFolder))]
    [ValidParent(ParentType = typeof(Folder))]
    [Description("This report automatically generates a current balance column for each CLEM Resource Type\nassociated with the CLEM Resource Groups specified (name only) in the variable list.")]
    [Version(1, 0, 1, "")]
    public class ReportResourceBalances: Models.Report.Report
    {
        [Link]
        private ResourcesHolder Resources = null;

        /// <summary>The columns to write to the data store.</summary>
        private List<IReportColumn> columns = null;

        /// <summary>An array of column names to write to storage.</summary>
        private IEnumerable<string> columnNames = null;

        /// <summary>An array of columns units to write to storage.</summary>
        private IEnumerable<string> columnUnits = null;

        /// <summary>Link to a simulation</summary>
        [Link]
        private Simulation simulation = null;

        /// <summary>Link to a clock model.</summary>
        [Link]
        private IClock clock = null;

        /// <summary>Link to a storage service.</summary>
        [Link]
        private IStorageWriter storage = null;

        /// <summary>Link to a locator service.</summary>
        [Link]
        private ILocator locator = null;

        /// <summary>Link to an event service.</summary>
        [Link]
        private IEvent events = null;

        /// <summary>An event handler to allow us to initialize ourselves.</summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        [EventSubscribe("Commencing")]
        private void OnCommencing(object sender, EventArgs e)
        {
            // sanitise the variable names and remove duplicates
            List<string> variableNames = new List<string>();
            variableNames.Add("Parent.Name as Zone");

            if(VariableNames.Where(a => a.Contains("[Clock].Today")).Count() == 0)
            {
                variableNames.Add("[Clock].Today as Date");
            }

            if (VariableNames != null)
            {
                for (int i = 0; i < this.VariableNames.Length; i++)
                {
                    // each variable name is now a ResourceGroup
                    bool isDuplicate = StringUtilities.IndexOfCaseInsensitive(variableNames, this.VariableNames[i].Trim()) != -1;
                    if (!isDuplicate && this.VariableNames[i] != string.Empty)
                    {
                        if (this.VariableNames[i].StartsWith("["))
                        {
                            variableNames.Add(this.VariableNames[i]);
                        }
                        else
                        {
                            // check it is a ResourceGroup
                            CLEMModel model = Resources.GetResourceGroupByName(this.VariableNames[i]) as CLEMModel;
                            if (model == null)
                            {
                                throw new ApsimXException(this, String.Format("@error:Invalid resource group [r={0}] in ReportResourceBalances [{1}]\nEntry has been ignored", this.VariableNames[i], this.Name));
                            }
                            else
                            {
                                if (model.GetType().Name == "Labour")
                                {
                                    for (int j = 0; j < (model as Labour).Items.Count; j++)
                                    {
                                        variableNames.Add("[Resources]." + this.VariableNames[i] + ".Items[" + j.ToString() + "].AvailableDays as " + (model as Labour).Items[j].Name);
                                    }
                                }
                                else
                                {
                                    // get all children
                                    foreach (CLEMModel item in model.Children.Where(a => a.GetType().IsSubclassOf(typeof(CLEMModel)))) // Apsim.Children(this, typeof(CLEMModel))) //
                                    {
                                        string amountStr = "Amount";
                                        switch (item.GetType().Name)
                                        {
                                            case "FinanceType":
                                                amountStr = "Balance";
                                                break;
                                            case "LabourType":
                                                amountStr = "AvailableDays";
                                                break;
                                            default:
                                                break;
                                        }
                                        variableNames.Add("[Resources]." + this.VariableNames[i] + "." + item.Name + "." + amountStr + " as " + item.Name);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            base.VariableNames = variableNames.ToArray();
            this.FindVariableMembers();

            if (EventNames[0] == "")
            {
                events.Subscribe("[Clock].CLEMEndOfTimeStep", DoOutputEvent);
            }
            else
            {
                // Subscribe to events.
                foreach (string eventName in EventNames)
                {
                    if (eventName != string.Empty)
                    {
                        events.Subscribe(eventName.Trim(), DoOutputEvent);
                    }
                }
            }
        }

        /// <summary>A method that can be called by other models to perform a line of output.</summary>
        public new void DoOutput()
        {
            object[] valuesToWrite = new object[columns.Count];
            for (int i = 0; i < columns.Count; i++)
            {
                valuesToWrite[i] = columns[i].GetValue();
            }

            storage.WriteRow(simulation.Name, Name, columnNames, columnUnits, valuesToWrite);
        }

        /// <summary>Create a text report from tables in this data store.</summary>
        /// <param name="storage">The data store.</param>
        /// <param name="fileName">Name of the file.</param>
        public static new void WriteAllTables(IStorageReader storage, string fileName)
        {
            // Write out each table for this simulation.
            foreach (string tableName in storage.TableNames)
            {
                DataTable data = storage.GetData(tableName);
                if (data != null && data.Rows.Count > 0)
                {
                    SortColumnsOfDataTable(data);
                    StreamWriter report = new StreamWriter(Path.ChangeExtension(fileName, "." + tableName + ".csv"));
                    DataTableUtilities.DataTableToText(data, 0, ",", true, report);
                    report.Close();
                }
            }
        }

        /// <summary>Sort the columns alphabetically</summary>
        /// <param name="table">The table to sort</param>
        private static void SortColumnsOfDataTable(DataTable table)
        {
            var columnArray = new DataColumn[table.Columns.Count];
            table.Columns.CopyTo(columnArray, 0);
            var ordinal = -1;
            foreach (var orderedColumn in columnArray.OrderBy(c => c.ColumnName))
            {
                orderedColumn.SetOrdinal(++ordinal);
            }

            ordinal = -1;
            int i = table.Columns.IndexOf("SimulationName");
            if (i != -1)
            {
                table.Columns[i].SetOrdinal(++ordinal);
            }

            i = table.Columns.IndexOf("SimulationID");
            if (i != -1)
            {
                table.Columns[i].SetOrdinal(++ordinal);
            }
        }


        /// <summary>Called when one of our 'EventNames' events are invoked</summary>
        public new void DoOutputEvent(object sender, EventArgs e)
        {
            DoOutput();
        }

        /// <summary>
        /// Fill the Members list with VariableMember objects for each variable.
        /// </summary>
        private void FindVariableMembers()
        {
            this.columns = new List<IReportColumn>();

            AddExperimentFactorLevels();

            foreach (string fullVariableName in this.VariableNames)
            {
                if (fullVariableName != string.Empty)
                {
                    this.columns.Add(ReportColumn.Create(fullVariableName, clock, storage, locator, events));
                }
            }
            columnNames = columns.Select(c => c.Name);
            columnUnits = columns.Select(c => c.Units);
        }

        /// <summary>Add the experiment factor levels as columns.</summary>
        private void AddExperimentFactorLevels()
        {
            if (ExperimentFactorValues != null)
            {
                for (int i = 0; i < ExperimentFactorNames.Count; i++)
                {
                    this.columns.Add(new ReportColumnConstantValue(ExperimentFactorNames[i], ExperimentFactorValues[i]));
                }
            }
        }

    }
}

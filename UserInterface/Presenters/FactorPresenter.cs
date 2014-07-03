﻿using System;
using Models.Factorial;
using UserInterface.Views;
using System.Reflection;
using System.Collections.Generic;
using Models.Core;
namespace UserInterface.Presenters
{
    /// <summary>
    /// Connects a Factor model to a FactorView.
    /// </summary>
    public class FactorPresenter : IPresenter
    {
        private Factor Factor;
        private IFactorView FactorView;
        private ExplorerPresenter ExplorerPresenter;

        public void Attach(object model, object view, ExplorerPresenter explorerPresenter)
        {
            Factor = model as Factor;
            FactorView = view as IFactorView;
            ExplorerPresenter = explorerPresenter;

            FactorView.Editor.Lines = Factor.Paths.ToArray();

            FactorView.Editor.TextHasChangedByUser += OnTextHasChangedByUser;
            FactorView.Editor.ContextItemsNeeded += OnContextItemsNeeded;
            ExplorerPresenter.CommandHistory.ModelChanged += OnModelChanged;
        }


        public void Detach()
        {
            FactorView.Editor.TextHasChangedByUser -= OnTextHasChangedByUser;
            FactorView.Editor.ContextItemsNeeded -= OnContextItemsNeeded;
            ExplorerPresenter.CommandHistory.ModelChanged -= OnModelChanged;            
        }

        /// <summary>
        /// Intellisense lookup.
        /// </summary>
        void OnContextItemsNeeded(object sender, Utility.NeedContextItems e)
        {
            if (e.ObjectName == "")
                e.ObjectName = ".";
            object o = Factor.Variables.Get(e.ObjectName);

            if (o != null)
            {
                foreach (IVariable Property in ModelFunctions.FieldsAndProperties(o, BindingFlags.Instance | BindingFlags.Public))
                    e.Items.Add(Property.Name);
                e.Items.Sort();
            }
        }

        /// <summary>
        /// User has changed the paths. Save to model.
        /// </summary>
        void OnTextHasChangedByUser(object sender, EventArgs e)
        {
            ExplorerPresenter.CommandHistory.ModelChanged -= OnModelChanged;

            List<string> newPaths = new List<string>();
            newPaths.AddRange(FactorView.Editor.Lines);
            ExplorerPresenter.CommandHistory.Add(new Commands.ChangePropertyCommand(Factor, "Paths", newPaths));

            ExplorerPresenter.CommandHistory.ModelChanged += OnModelChanged;
        }


        /// <summary>
        /// The model has changed probably by an undo.
        /// </summary>
        void OnModelChanged(object changedModel)
        {
            FactorView.Editor.Lines = Factor.Paths.ToArray();
        }


    }
}

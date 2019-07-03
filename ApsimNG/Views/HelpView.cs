﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Gtk;

namespace UserInterface.Views
{
    public class HelpView : ViewBase
    {
        /// <summary>
        /// Window in which help info is displayed.
        /// </summary>
        private Dialog window;

        /// <summary>
        /// Reference to the main view.
        /// </summary>
        private MainView parent;

        /// <summary>
        /// Label containing link to the next gen website.
        /// </summary>
        private LinkButton website;

        /// <summary>
        /// Constructor. Initialises the view.
        /// </summary>
        /// <param name="owner"></param>
        public HelpView(MainView owner) : base(owner)
        {
            window = new Dialog("Help", owner.MainWidget as Window, DialogFlags.DestroyWithParent | DialogFlags.Modal | DialogFlags.NoSeparator);
            window.WindowPosition = WindowPosition.Center;
            window.DeleteEvent += OnDelete;
            window.Destroyed += OnClose;

            VBox container = new VBox(true, 10);

            Frame websiteFrame = new Frame("Website");
            website = new LinkButton("https://apsimnextgeneration.netlify.com", "Apsim Next Generation Website");
            website.Clicked += OnWebsiteClicked;
            websiteFrame.Add(website);
            container.PackStart(websiteFrame, false, false, 0);

            Frame citationFrame = new Frame("Acknowledgement");
            Label citation = new Label();
            citation.UseMarkup = true;
            citation.Wrap = true;
            string citationRule = @"<b>APSIM Next Generation citation:</b>

Holzworth, Dean, N.I.Huth, J.Fainges, H.Brown, E.Zurcher, R.Cichota, S.Verrall, N.I.Herrmann, B.Zheng, and V.Snow. “APSIM Next Generation: Overcoming Challenges in Modernising a Farming Systems Model.” Environmental Modelling & Software 103(May 1, 2018): 43–51.https://doi.org/10.1016/j.envsoft.2018.02.002.

<b>APSIM Acknowledgement</b>

The APSIM Initiative would appreciate an acknowledgement in your research paper if you or your team have utilised APSIM in its development. For ease, we suggest the following wording:

<i>Acknowledgment is made to the APSIM Initiative which takes responsibility for quality assurance and a structured innovation programme for APSIM's modelling software, which is provided free for research and development use (see www.apsim.info for details)</i>";
            citation.Markup = citationRule;
            citationFrame.Add(citation);
            container.PackStart(citationFrame, false, false, 0);

            window.AddActionWidget(container, ResponseType.None);

            mainWidget = window;
        }

        /// <summary>
        /// Invoked when the user clicks on the link to the website.
        /// </summary>
        /// <param name="sender">Sender object.</param>
        /// <param name="e">Event Arguments.</param>
        private void OnWebsiteClicked(object sender, EventArgs e)
        {
            try
            {
                Process.Start(website.Uri);
            }
            catch (Exception err)
            {
                ShowError(err);
            }
        }

        /// <summary>
        /// Controls the visibility of the view.
        /// Settings this to true displays the view.
        /// </summary>
        public bool Visible
        {
            get
            {
                return window.Visible;
            }
            set
            {
                if (value)
                    window.ShowAll();
                else
                    window.HideAll();
            }
        }

        /// <summary>
        /// Invoked when the user closes the window.
        /// This prevents the window from closing, but still hides
        /// the window. This means we don't have to re-initialise
        /// the window each time the user opens it.
        /// </summary>
        /// <param name="sender">Sender object.</param>
        /// <param name="args">Event arguments.</param>
        [GLib.ConnectBefore]
        private void OnDelete(object sender, DeleteEventArgs args)
        {
            try
            {
                Visible = false;
                args.RetVal = true;
            }
            catch (Exception err)
            {
                ShowError(err);
            }
        }

        /// <summary>
        /// Invoked when the window is closed for good, when Apsim closes.
        /// </summary>
        /// <param name="sender">Event arguments.</param>
        /// <param name="args">Sender object.</param>
        [GLib.ConnectBefore]
        private void OnClose(object sender, EventArgs args)
        {
            try
            {
                website.Clicked += OnWebsiteClicked;
                window.DeleteEvent -= OnDelete;
                window.Destroyed -= OnClose;
                window.Dispose();
            }
            catch (Exception err)
            {
                ShowError(err);
            }
        }
    }
}

#region Namespaces
using System;
using System.Collections.Generic;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit;
using System.Linq;
using System.Reflection;
using System.IO;
using Autodesk.Revit.DB.Events;
#endregion

/* Copyright © 2011, Tzvi A. Friedman
 * All rights reserved.
 * 
 * License
 * This file is part of "Revit PhaseGraphics Addin".

    The "Revit PhaseGraphics Addin" is free software: you can redistribute it and/or modify
    it under the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    The "Revit PhaseGraphics Addin" is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with the "Revit PhaseGraphics Addin".  If not, see <http://www.gnu.org/licenses/>.
 */

//DESCRIPTION
//Works in the background to add a PhaseGraphics Parameter to Walls and other elements so they can be changed with Filters
namespace PhaseSyncAddin
{
    
    class App : IExternalApplication
    {
        private const string PhaseGraphicsGUID = "e0e0252a-0adb-4066-bdf0-d756494b9c72";
        public Result OnStartup(UIControlledApplication a)
        {
            //add new event to fire when a document opens
            a.ControlledApplication.DocumentOpened += new EventHandler<Autodesk.Revit.DB.Events.DocumentOpenedEventArgs>(application_DocumentOpened);


            // Register updater with Revit
            PhaseGraphicsUpdater updater = new PhaseGraphicsUpdater(a.ActiveAddInId);
            UpdaterRegistry.RegisterUpdater(updater);

            ElementMulticlassFilter Filter = PhaseGraphicsTypeFilter();
            UpdaterRegistry.AddTrigger(updater.GetUpdaterId(), Filter, Element.GetChangeTypeAny());
             return Result.Succeeded;
        }

        private static ElementMulticlassFilter PhaseGraphicsTypeFilter()
        {
            List<Type> types = new List<Type>(4);

            types.Add(typeof(ContFooting));
            types.Add(typeof(Wall));
            types.Add(typeof(Floor));
            types.Add(typeof(RoofBase));
            ElementMulticlassFilter Filter = new ElementMulticlassFilter(types);
            return Filter;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            PhaseGraphicsUpdater updater = new PhaseGraphicsUpdater(a.ActiveAddInId);
            UpdaterRegistry.UnregisterUpdater(updater.GetUpdaterId());
            return Result.Succeeded;
        }
        public void application_DocumentOpened(object sender, DocumentOpenedEventArgs args)
        { 
            Document doc = args.Document;
            //add new parameter when a document opens
            Transaction transaction = new Transaction(doc, "Add PhaseGraphics");
            if (transaction.Start() == TransactionStatus.Started)
            {
                var fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Autodesk\Revit\Addins\2012\PhaseSyncSharedParams.txt");
                SetNewParameterToInstances(doc, fileName.ToString()); 
                transaction.Commit();
            }
            //sync phasegraphics param with Phases of all empty PhaseGraphics objects
            Transaction transaction2 = new Transaction(doc, "Sync PhaseGraphics");
            ICollection<Element> types = null;
            if (transaction2.Start() == TransactionStatus.Started)
            {

                  // Apply the filter to the elements in the active document
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                types = collector.WherePasses(PhaseGraphicsTypeFilter()).ToElements();
                foreach (Element elem in types)
                    
                { 
                    //get the phasegraphics parameter from its guid 
                    if (elem.get_Parameter(new Guid(PhaseGraphicsGUID)).HasValue == false)
                    {
                        SyncPhaseGraphics(doc, elem);
                    }

                }
                transaction2.Commit();
            }
 
           
        }


        #region Shared Parameter
        public bool SetNewParameterToInstances(Document doc, string sharedParameterFile)
        {
            Application app = doc.Application;
            DefinitionFile myDefinitionFile;
            // set the path of shared parameter file to current Revit
            app.SharedParametersFilename = sharedParameterFile;
            // open the file
            myDefinitionFile = app.OpenSharedParameterFile();
            // get the phasegraphics param
            Definition PhaseGraphics = myDefinitionFile.Groups.get_Item("Phase").Definitions.get_Item("PhaseGraphics");
            CategorySet myCategories = app.Create.NewCategorySet();
             //use BuiltInCategory to add categories for phasegraphics
            AddCategories(doc, myCategories,BuiltInCategory.OST_Walls);
            AddCategories(doc, myCategories, BuiltInCategory.OST_Roofs);
            AddCategories(doc, myCategories, BuiltInCategory.OST_Floors);
            AddCategories(doc, myCategories, BuiltInCategory.OST_StructuralFoundation);
            //Create an instance of InstanceBinding
            InstanceBinding instanceBinding = app.Create.NewInstanceBinding(myCategories);
            BindingMap bindingMap = doc.ParameterBindings;
            // Bind the definitions to the document
            bool instanceBindOK = bindingMap.Insert(PhaseGraphics, instanceBinding, BuiltInParameterGroup.PG_TEXT);
            
            return instanceBindOK;
        }

        private static void AddCategories(Document doc, CategorySet myCategories, BuiltInCategory cat)
        {
            Category myCategory = doc.Settings.Categories.get_Item(cat);
            myCategories.Insert(myCategory);
        }

        #endregion


        public class PhaseGraphicsUpdater : IUpdater
        {
            static AddInId m_appId;
            static UpdaterId m_updaterId;
            WallType m_wallType = null;
            // constructor takes the AddInId for the add-in associated with this updater
            public PhaseGraphicsUpdater(AddInId id)
            {
                m_appId = id;
                m_updaterId = new UpdaterId(m_appId, new Guid(PhaseGraphicsGUID));
            }

            public void Execute(UpdaterData data)
            {
                
                Document doc = data.GetDocument();
                    //sync the modified elements
                    foreach (ElementId ElemId in data.GetModifiedElementIds())
                    {
                        SyncPhaseGraphics(doc, ElemId);
                   
                    }
                    //sync the added elements
                    foreach (ElementId ElemId in data.GetAddedElementIds())
                    {
                        SyncPhaseGraphics(doc, ElemId);
                    }

            }


            public string GetAdditionalInformation()
            {
                return "Syncs Phases with PhaseGraphics for all walls,floors,foundations,roofs changed,";
            }
            public ChangePriority GetChangePriority()
            {
                return ChangePriority.FloorsRoofsStructuralWalls;
            }
            public UpdaterId GetUpdaterId()
            {
                return m_updaterId;
            }
            public string GetUpdaterName()
            {
                return "Phase Graphics Updater";
            }
        } // end updater
        //sync the PhaseGraphics with one overload taking an already expanded elemnt to save time
        private static void SyncPhaseGraphics(Document doc, ElementId ElemId)
        {
            Element e = doc.get_Element(ElemId);
            SyncPhaseGraphics(doc, e);
        }
        private static void SyncPhaseGraphics(Document doc, Element elem)
        {
            

            if (elem != null)
            {

                var phase = from Parameter p in elem.Parameters where p.Definition.Name == "PhaseGraphics" select p;
                if ((phase.Count() > 0) && (phase.First<Parameter>().AsString() != elem.PhaseCreated.Name))
                {
                    phase.First<Parameter>().Set(elem.PhaseCreated.Name);
                }

            }
        }
    }
}

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
//DESCRIPTION
//Works in the background to add a PhaseGraphics Parameter to Walls and other elements so they can be changed with Filters
namespace PhaseSyncAddin
{
    class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication a)
        {//add new parameter to wall when a document opens
            a.ControlledApplication.DocumentOpened += new EventHandler<Autodesk.Revit.DB.Events.DocumentOpenedEventArgs>(application_DocumentOpened);


            // Register wall updater with Revit
            WallUpdater updater = new WallUpdater(a.ActiveAddInId);
            UpdaterRegistry.RegisterUpdater(updater);
            // Change Scope = any Wall element
            ElementClassFilter wallFilter = new ElementClassFilter(typeof(Wall));
            // Change type = element addition
            
            UpdaterRegistry.AddTrigger(updater.GetUpdaterId(), wallFilter, Element.GetChangeTypeParameter(new ElementId(BuiltInParameter.PHASE_CREATED )));
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            WallUpdater updater = new WallUpdater(a.ActiveAddInId);
            UpdaterRegistry.UnregisterUpdater(updater.GetUpdaterId());
            return Result.Succeeded;
        }
        public void application_DocumentOpened(object sender, DocumentOpenedEventArgs args)
        { // get document from event args.
            Document doc = args.Document;
            //add new parameter to wall when a document opens
            Transaction transaction = new Transaction(doc, "Add PhaseGraphics");
            if (transaction.Start() == TransactionStatus.Started)
            {
                SetNewParameterToInstanceWall(doc, @"C:\Users\tzvi\AppData\Roaming\Autodesk\Revit\Addins\2012\PhaseSyncSharedParams.txt");  // AssemblyDirectory + @"\PhaseSyncSharedParams.txt");
                transaction.Commit();
            }
           

        }


        #region Shared Parameter
        public bool SetNewParameterToInstanceWall(Document doc, string sharedParameterFile)
        {
            Application app = doc.Application;
            DefinitionFile myDefinitionFile;
            // set the path of shared parameter file to current Revit
            app.SharedParametersFilename = sharedParameterFile;
            // open the file
            myDefinitionFile = app.OpenSharedParameterFile();
            // create a new group in the shared parameters file
            DefinitionGroups myGroups = myDefinitionFile.Groups;
            DefinitionGroup myGroup = myGroups.Create("Phase");
            // create an instance definition in definition group MyParameters
            Definition PhaseGraphics = myGroup.Definitions.Create("PhaseGraphics", ParameterType.Text);
            // create a category set and insert category of wall to it
            CategorySet myCategories = app.Create.NewCategorySet();
            // use BuiltInCategory to get category of wall
            Category myCategory = doc.Settings.Categories.get_Item(
            BuiltInCategory.OST_Walls);
            myCategories.Insert(myCategory);
            //Create an instance of InstanceBinding
            InstanceBinding instanceBinding = app.Create.NewInstanceBinding(myCategories);
            // Get the BindingMap of current document.
            BindingMap bindingMap = doc.ParameterBindings;
            // Bind the definitions to the document
            bool instanceBindOK = bindingMap.Insert(PhaseGraphics, instanceBinding, BuiltInParameterGroup.PG_TEXT);
            return instanceBindOK;

        }

        //static public string AssemblyDirectory
        //{
        //    get
        //    {
        //        string codeBase = Assembly.GetExecutingAssembly().CodeBase;
        //        UriBuilder uri = new UriBuilder(codeBase);
        //        string path = Uri.UnescapeDataString(uri.Path);
        //        return Path.GetDirectoryName(path);
        //    }
        //}
        #endregion


        public class WallUpdater : IUpdater
        {
            static AddInId m_appId;
            static UpdaterId m_updaterId;
            WallType m_wallType = null;
            // constructor takes the AddInId for the add-in associated with this updater
            public WallUpdater(AddInId id)
            {
                m_appId = id;
                m_updaterId = new UpdaterId(m_appId, new Guid("0D1AD583-A9F0-43D5-9680-C99427DC0D25"));
            }
            public void Execute(UpdaterData data)
            {
                
                Document doc = data.GetDocument();
                foreach (ElementId addedElemId in data.GetAddedElementIds())
                    {
                        Wall wall = doc.get_Element(addedElemId) as Wall;
                        // TODO: add some error  checking code
                        if (wall != null)
                        {
                            var phase = from Parameter p in wall.Parameters where p.Definition.Name == "PhaseGraphics" select p;
                            phase.First<Parameter>().Set(wall.PhaseCreated.Name);
                            TaskDialog.Show("Revit", wall.PhaseCreated.Name + phase.First<Parameter>().AsString() );
                        }
                        
                    }
                // Cache the wall type
                //if (m_wallType == null)
                //{
                //    FilteredElementCollector collector = new FilteredElementCollector(doc);
                //    collector.OfClass(typeof(WallType));
                //    //var wallTypes = from element in collector
                //    //                where
                //    //                element.Name == "Exterior - Brick on CMU"
                //    //                select element;
                //    //if (wallTypes.Count<Element>() > 0)
                //    //{
                //    //    m_wallType = wallTypes.Cast<WallType>().ElementAt<WallType>(0);
                //    //}
                //}
                //if (m_wallType != null)
                //{
                //    // Change the wall to the cached wall type.
                    
                //}

            }
            public string GetAdditionalInformation()
            {
                return "Syncs Phases with PhaseGraphics for all walls changed,";
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
                return "Wall Phase Graphics Updater";
            }
        }
    }
}

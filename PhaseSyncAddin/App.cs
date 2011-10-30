#region Namespaces
using System;
using System.Collections.Generic;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

#endregion

namespace PhaseSyncAddin
{
    class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication a)
        {
            // Register wall updater with Revit
            WallUpdater updater = new WallUpdater(a.ActiveAddInId);
            UpdaterRegistry.RegisterUpdater(updater);
            // Change Scope = any Wall element
            ElementClassFilter wallFilter = new ElementClassFilter(typeof(Wall));
            // Change type = element addition
            UpdaterRegistry.AddTrigger(updater.GetUpdaterId(), wallFilter,
            Element.GetChangeTypeElementAddition());
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            WallUpdater updater = new WallUpdater(a.ActiveAddInId);
            UpdaterRegistry.UnregisterUpdater(updater.GetUpdaterId());
            return Result.Succeeded;
        }
        #region Shared Parameter
        public bool SetNewParameterToInsanceWall(UIApplication app, DefinitionFile myDefinitionFile)
        {
            // create a new group in the shared parameters file
            DefinitionGroups myGroups = myDefinitionFile.Groups;
            DefinitionGroup myGroup = myGroups.Create("MyParameters1");
            // create an instance definition in definition group MyParameters
            Definition myDefinition_ProductDate =
            myGroup.Definitions.Create("Instance_ProductDate", ParameterType.Text);
            // create a category set and insert category of wall to it
            CategorySet myCategories = app.Application.Create.NewCategorySet();
            // use BuiltInCategory to get category of wall
            Category myCategory = app.ActiveUIDocument.Document.Settings.Categories.get_Item(
            BuiltInCategory.OST_Walls);
            myCategories.Insert(myCategory);
            //Create an instance of InstanceBinding
            InstanceBinding instanceBinding =
            app.Application.Create.NewInstanceBinding(myCategories);
            // Get the BingdingMap of current document.
            BindingMap bindingMap = app.ActiveUIDocument.Document.ParameterBindings;
            // Bind the definitions to the document
            bool instanceBindOK = bindingMap.Insert(myDefinition_ProductDate,
            instanceBinding, BuiltInParameterGroup.PG_TEXT);
            return instanceBindOK;
        }
        private DefinitionFile SetAndOpenExternalSharedParamFile(
Autodesk.Revit.ApplicationServices.Application application, string sharedParameterFile)
        {
            // set the path of shared parameter file to current Revit
            application.Options.SharedParametersFilename = sharedParameterFile;
            // open the file
            return application.OpenSharedParameterFile();
        }
        #endregion
    }

    public class WallUpdater : IUpdater
    {
        static AddInId m_appId;
        static UpdaterId m_updaterId;
        WallType m_wallType = null;
        // constructor takes the AddInId for the add-in associated with this updater
        public WallUpdater(AddInId id)
        {
            m_appId = id;
            m_updaterId = new UpdaterId(m_appId, new Guid("3b27d482-9fde-45aa-abd8-b814d5b005f0"));
        }
        public void Execute(UpdaterData data)
        {
            Document doc = data.GetDocument();
            // Cache the wall type
            if (m_wallType == null)
            {
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                collector.OfClass(typeof(WallType));
                var wallTypes = from element in collector
                                where
                                element.Name == "Exterior - Brick on CMU"
                                select element;
                if (wallTypes.Count<Element>() > 0)
                {
                    m_wallType = wallTypes.Cast<WallType>().ElementAt<WallType>(0);
                }
            }
            if (m_wallType != null)
            {
                // Change the wall to the cached wall type.
                foreach (ElementId addedElemId in data.GetAddedElementIds())
                {
                    Wall wall = doc.get_Element(addedElemId) as Wall;

                    if (wall != null)
                    {
                        wall.WallType = m_wallType;
                    }
                }
            }

        }
        public string GetAdditionalInformation()
        {
            return "Wall type updater example: updates all newly created walls to a special wall";
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
            return "Wall Type Updater";
        }
    }

}

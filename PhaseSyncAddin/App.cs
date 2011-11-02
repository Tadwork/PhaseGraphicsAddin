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


            // Register updater with Revit
            WallUpdater updater = new WallUpdater(a.ActiveAddInId);
            UpdaterRegistry.RegisterUpdater(updater);

            ElementMulticlassFilter Filter = PhaseGraphicsTypeFilter();
           // ElementClassFilter wallFilter = new ElementClassFilter(typeof(Wall));
            // Change type = element addition
            UpdaterRegistry.AddTrigger(updater.GetUpdaterId(), Filter, Element.GetChangeTypeElementAddition());
           // UpdaterRegistry.AddTrigger(updater.GetUpdaterId(), wallFilter, Element.GetChangeTypeParameter(new ElementId(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)));           
            UpdaterRegistry.AddTrigger(updater.GetUpdaterId(), Filter,Element.GetChangeTypeParameter(new ElementId(BuiltInParameter.PHASE_CREATED )));
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
                
                SetNewParameterToInstanceWall(doc, @"C:\Users\tzvi\AppData\Roaming\Autodesk\Revit\Addins\2012\PhaseSyncSharedParams.txt"); //AssemblyDirectory + @"\PhaseSyncSharedParams.txt");
                transaction.Commit();
            }

            transaction = new Transaction(doc, "Sync PhaseGraphics");
            if (transaction.Start() == TransactionStatus.Started)
            {

                // Create a Filter to get all the doors in the document
                //ElementClassFilter familyInstanceFilter = new ElementClassFilter(typeof(FamilyInstance));
                // Creates an ElementParameter filter to find rooms whose area is 
                // greater than specified value
                // Create filter by provider and evaluator 
                // provider
                ElementMulticlassFilter typeFilters = PhaseGraphicsTypeFilter();
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                ICollection<ElementId> types = collector.WherePasses(typeFilters).ToElementIds();
//                Element e = doc.get_Element(types.First<ElementId>());
//                TaskDialog.Show("REvit", types.Count().ToString());
//                Parameter phasegraphicsParam = e.get_Parameter(new Guid("e0e0252a-0adb-4066-bdf0-d756494b9c72"));
//                ParameterValueProvider pvp = new ParameterValueProvider(phasegraphicsParam.Id);

//#region not empty
//                        // evaluator
//                FilterStringRuleEvaluator ruleEval = new  FilterStringEquals();
//                // rule value    
//                //string ruleValue = ""; // filter room whose value is empty
//                // rule
//                FilterRule fRule = new FilterStringRule(pvp,ruleEval,string.Empty,false);

//                // Create an ElementParameter filter
//                ElementParameterFilter paramFilternotempty = new ElementParameterFilter(fRule,true); 
//    #endregion

//                ICollection<ElementId> notEmptyPhaseGraphics = collector.WherePasses(paramFilternotempty).ToElementIds();
//                Parameter p = doc.get_Element(notEmptyPhaseGraphics.First<ElementId>()).get_Parameter(new Guid("e0e0252a-0adb-4066-bdf0-d756494b9c72"));
//                TaskDialog.Show("REvit", notEmptyPhaseGraphics.Count().ToString() + p.HasValue + "'" + p.AsString() + "'");
//                //// Apply the filter to the elements in the active document
//                //FilteredElementCollector collector = new FilteredElementCollector(doc);
//                //IList<Element> rooms = collector.WherePasses(filter).ToElements();


//                //// Find rooms whose area is less than or equal to 100: 
//                //// Use inverted filter to match elements
//                //ElementParameterFilter lessOrEqualFilter = new ElementParameterFilter(fRule, true);
//                //collector = new FilteredElementCollector(document);
//                //IList<Element> lessOrEqualFounds = collector.WherePasses(lessOrEqualFilter).ToElements();
//                ExclusionFilter ExcludenotEmpty = new ExclusionFilter(notEmptyPhaseGraphics);
//                LogicalAndFilter filter = new LogicalAndFilter(typeFilters, ExcludenotEmpty);
//                ICollection<ElementId> EmptyPhaseGraphics = collector.WherePasses(filter).ToElementIds();
//                TaskDialog.Show("REvit", EmptyPhaseGraphics.Count().ToString());
                foreach (ElementId ElemId in types)
                    
                { 
                    Element elem = doc.get_Element(ElemId);
                    if (elem.get_Parameter(new Guid("e0e0252a-0adb-4066-bdf0-d756494b9c72")).HasValue == false)
                    {
                        SyncPhaseGraphics(doc, elem);
                    }

                }
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
            // create a category set and insert category of wall to it
            Definition PhaseGraphics = myDefinitionFile.Groups.get_Item("Phase").Definitions.get_Item("PhaseGraphics");
            CategorySet myCategories = app.Create.NewCategorySet();
             //use BuiltInCategory to get category of wall
            AddCategories(doc, myCategories,BuiltInCategory.OST_Walls);
            AddCategories(doc, myCategories, BuiltInCategory.OST_Roofs);
            AddCategories(doc, myCategories, BuiltInCategory.OST_Floors);
            AddCategories(doc, myCategories, BuiltInCategory.OST_StructuralFoundation);
            //Create an instance of InstanceBinding
            InstanceBinding instanceBinding = app.Create.NewInstanceBinding(myCategories);
            // Get the BindingMap of current document.
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

        static public string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }
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
                foreach (ElementId ElemId in data.GetModifiedElementIds())
                    {
                        SyncPhaseGraphics(doc, ElemId);
                   
                    }
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
        }
        private static void SyncPhaseGraphics(Document doc, ElementId ElemId)
        {
            Element e = doc.get_Element(ElemId);
            // TODO: add some error  checking code
            if (e != null)
            {


                var phase = from Parameter p in e.Parameters where p.Definition.Name == "PhaseGraphics" select p;
                phase.First<Parameter>().Set(e.PhaseCreated.Name);

            }
        }
        private static void SyncPhaseGraphics(Document doc, Element Elem)
        {
            Element e = Elem;
            // TODO: add some error  checking code
            if (e != null)
            {


                var phase = from Parameter p in e.Parameters where p.Definition.Name == "PhaseGraphics" select p;
                phase.First<Parameter>().Set(e.PhaseCreated.Name);

            }
        }
    }
}

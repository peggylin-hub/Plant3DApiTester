using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

using Autodesk.ProcessPower.ProjectManager;
using Autodesk.ProcessPower.PlantInstance;
using Autodesk.ProcessPower.DataLinks;
using Autodesk.ProcessPower.P3dProjectParts;
using Autodesk.ProcessPower.PnP3dObjects;
using Autodesk.ProcessPower.AcPp3dObjectsUtils;

using DataLinksManager = Autodesk.ProcessPower.DataLinks.DataLinksManager;
using Autodesk.AutoCAD.ApplicationServices;
using System.ComponentModel;
using System.IO;
using System.Collections.Specialized;
using Autodesk.ProcessPower.PnP3dObjects;
using Autodesk.ProcessPower.DataObjects;

namespace Plant3DApiTester
{
    public class GetData
    {
        /// <summary>
        /// get set object property
        /// </summary>
        [CommandMethod("Retrive_Plant3DPart_data")]
        public void Retrive_Plant3DPart_data()
        {
            //file name to save different data 
            string partDataFromObjPath = @"C: \Users\LinP3\source\repos\Plant3DApiTester\Resource\partDataFromObj.txt";
            string partDataFromDBPath = @"C: \Users\LinP3\source\repos\Plant3DApiTester\Resource\partDataFromDB.txt";
           
            ///all available drawing type
            string DwgType_Piping = "Piping";
            string DwgType_PnId = "PnId";
            string DwgType_Ortho = "Ortho";
            string DwgType_Iso = "Iso";

            //get current drawing
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            ///create a clean file, prepare to write
            File.WriteAllText(partDataFromObjPath, $"{doc.Name}\n");
            File.WriteAllText(partDataFromDBPath, $"{doc.Name}\n");

            //get current project
            PlantProject mainPrj = PlantApplication.CurrentProject;
            Project prj = mainPrj.ProjectParts[DwgType_Piping];
            DataLinksManager dlm = prj.DataLinksManager;

            //get current drawing type
            string dwgtype = PnPProjectUtils.GetActiveDocumentType();
            if (dwgtype != DwgType_PnId && dwgtype != DwgType_Piping)
            {  //"PnId", "Piping", "Ortho", "Iso"
                ed.WriteMessage("This drawing is not a P&ID or 3D Model in the current project.\n");
                return;
            }

            //method to get PnPDatabase for project
            PnPDatabase pnpDB = dlm.GetPnPDatabase();

            #region get all object in cad and check if it is plant 3d object, if it is, get its PnPId
            string msg = string.Empty;

            ///get just current project and the datalink
            Project prjPart = PnPProjectUtils.GetProjectPartForCurrentDocument();
            DataLinksManager dlmPart = prjPart.DataLinksManager;
            //PnPDatabase dbPart = dlmPart.GetPnPDatabase();

            //check all information
            msg = getHeaderText("check PnPTable");
            PnPRowIdArray aPid = dlmPart.SelectAcPpRowIds(db);
            int numberOfObjsInCurrentProj = aPid.Count();
            msg += "Number of objects in current project database =" + numberOfObjsInCurrentProj;
            int no = 1;
            foreach (int rid in aPid)
            {
                StringCollection sKeys = new StringCollection();
                sKeys.Add("Tag");
                sKeys.Add("Description");
                sKeys.Add("PartFamilyLongDesc");
                sKeys.Add("Line Number");
                StringCollection sVals = dlm.GetProperties(rid, sKeys, true);
                msg += $"\n[{no}]PnPId {rid} ?(rid), Tag = {sVals[0]} ({sVals[1]}) {sVals[2]} <{ sVals[3]}>.";
                no++;
            }
            File.AppendAllText(partDataFromDBPath, msg);
            #endregion

            #region to check all objects in current drawings
            PromptSelectionOptions pso = new PromptSelectionOptions();
            pso.MessageForAdding = "\nSelect " + dwgtype + " objects to <All>:";

            ///select object in drawing
            PromptSelectionResult result = ed.GetSelection(pso);
            if (result.Status == PromptStatus.Cancel)
                return;
            if (result.Status != PromptStatus.OK)
                result = ed.SelectAll();
            SelectionSet ss = result.Value;
            ObjectId[] objIds = ss.GetObjectIds();

            ///traves over all objects
            Autodesk.AutoCAD.DatabaseServices.TransactionManager tranMag = db.TransactionManager;
            int numberOfObjsInCurrentDrawing = objIds.Count();
            File.AppendAllText(partDataFromObjPath, $"Number of Plant element in Selection ={numberOfObjsInCurrentDrawing}");
            int numberOfPlantObjsInCurrentDrawing = 1;
            msg = getHeaderText("traves over all objects on selection");
            no = 1;
            using (Transaction tran = tranMag.StartTransaction())
            {
                foreach (ObjectId objId in objIds)
                {
                    DBObject obj = tran.GetObject(objId, OpenMode.ForRead);
                    msg += getHeaderText($"({no})ClassID = {obj.ClassID}");

                    //return if it is not a plant object
                    if (!dlm.HasLinks(objId))
                        continue;

                    Handle handle = objId.Handle;
                    File.AppendAllText(partDataFromObjPath, $"({no})Handle = {handle.Value}, handle string = {handle.ToString()}\n");/////////////////// handle string is what is shown in Navisworks

                    try
                    {
                        //ObjectIdCollection objectIdCollection = dlm.GetRelatedAcDbObjectIds(objId);
                        File.AppendAllText(partDataFromObjPath, $"({no})ObjectId = {objId}, Id ={obj.Id}, ClassID ={obj.ClassID}\n");

                        //find row id
                        int rowId = dlmPart.FindAcPpRowId(objId);
                        File.AppendAllText(partDataFromObjPath, $"({no})rowId ={rowId}\n");
                        dlmPart.GetObjectClassname(rowId);

                        //find ppobjectid
                        PpObjectIdArray ppObjectIds = dlmPart.FindAcPpObjectIds(rowId);
                        foreach (PpObjectId ppObjectId in ppObjectIds)
                        {
                            File.AppendAllText(partDataFromObjPath, $"({no})dbHandle = {ppObjectId.dbHandle}, DwgId = {ppObjectId.DwgId}\n");
                        }

                        ///get property
                        //properties to lookup
                        StringCollection props = new StringCollection();
                        props.Add("WBS_Level1");
                        props.Add("WBS_Level2");
                        props.Add("WBS_Level3");
                        props.Add("WBS_Level4");
                        props.Add("WBS_Level5");
                        props.Add("WBS_Level6");
                        props.Add("WBS_Level7");
                        props.Add("PnPGuid");
                        //properties values
                        StringCollection propsValue = dlmPart.GetProperties(objId, props, true);

                        //update value
                        for(int i=0; i<props.Count; i++)
                        {
                            ///set property when value is empty
                            if (string.IsNullOrEmpty(propsValue[i]))
                            {
                                propsValue[i] = $"WBS_Level{i}_Value";
                            }
                        }
                        ///set property
                        dlmPart.SetProperties(objId, props, propsValue);

                        ///print out to see
                        for (int i = 0; i < props.Count; i++)
                        {
                            File.AppendAllText(partDataFromObjPath, $"({no})props = {props[i]}, Value = {propsValue[i]}\n");
                        }

                        numberOfPlantObjsInCurrentDrawing++;
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex){ ed.WriteMessage(ex.ToString()); }

                    Part part = obj as Part;
                    if (part != null)
                    {
                        print(part, $"({no})Part", partDataFromObjPath);
                        File.AppendAllText(partDataFromObjPath, $"({no})GetType = {part.GetType()}\n");

                        ///type:
                        ///Autodesk.ProcessPower.PnP3dObjects.Connector
                        ///Autodesk.ProcessPower.PnP3dObjects.Pipe
                        ///Autodesk.ProcessPower.PnP3dObjects.PipeInlineAsset
                        ///Autodesk.ProcessPower.PnP3dObjects.Equipment
                        if (part.GetType() == typeof(Connector))
                        {
                            try
                            {
                                Connector item = part as Connector;
                                print(item, typeof(Connector).ToString(), partDataFromObjPath);
                                
                            }
                            catch { }
                        }
                        if (part.GetType() == typeof(Pipe))
                        {
                            try
                            {
                                Pipe item = part as Pipe;
                                print(item, typeof(Pipe).ToString(), partDataFromObjPath);
                            }
                            catch { }
                        }
                        if (part.GetType() == typeof(PipeInlineAsset))
                        {
                            try
                            {
                                PipeInlineAsset item = part as PipeInlineAsset;
                                print(item, typeof(PipeInlineAsset).ToString(), partDataFromObjPath);
                            }
                            catch { }
                        }
                    }

                    File.AppendAllText(partDataFromObjPath, $"({no})++++++++++++++++++++++++++++++++\n");
                    no++;
                }
            }

            File.AppendAllText(partDataFromObjPath, $"Number of Plant element in drawing ={numberOfPlantObjsInCurrentDrawing}");
            #endregion
        }

        [CommandMethod("Retrive_Project_Data")]
        public void Retrive_Project_Data()
        {
            //file name to save different data 
            string filePath = @"C: \Users\LinP3\source\repos\Plant3DApiTester\Resource\plant_3d_data.txt";
           
            ///all available drawing type
            string DwgType_Piping = "Piping";
            string DwgType_PnId = "PnId";
            string DwgType_Ortho = "Ortho";
            string DwgType_Iso = "Iso";

            //get current drawing
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;


            ///create a clean file, prepare to write
            File.WriteAllText(filePath, $"{doc.Name}\n");

            ///get user data
            var userdata = doc.UserData.Values;
            foreach (var d in userdata)
                File.AppendAllText(filePath, "user data:" + d.ToString());

            ///get application information
            File.AppendAllText(filePath, "application info? " + AcadApp.AcadApplication.ToString());

            ///last version
            var version = db.LastSavedAsVersion;
            File.AppendAllText(filePath, "version: "+ version);

            File.AppendAllText(filePath, "OriginalFileVersion: " + db.OriginalFileVersion);

            print(db, "db", filePath);
            print(doc, "doc", filePath);
            //get current project
            PlantProject mainPrj = PlantApplication.CurrentProject;
            Project pipingPrj = mainPrj.ProjectParts[DwgType_Piping];
            Project pnIdPrj = mainPrj.ProjectParts[DwgType_PnId];
            Project orthoPrj = mainPrj.ProjectParts[DwgType_Ortho];
            Project isoPrj = mainPrj.ProjectParts[DwgType_Iso];
            DataLinksManager dlm = pipingPrj.DataLinksManager;

            //get current drawing type
            string dwgtype = PnPProjectUtils.GetActiveDocumentType();
            if (dwgtype != DwgType_PnId && dwgtype != DwgType_Piping)
            {  //"PnId", "Piping", "Ortho", "Iso"
                ed.WriteMessage("This drawing is not a P&ID or 3D Model in the current project.\n");
                return;
            }

            //print information about main Project and Piping Project
            print(mainPrj, "mainPrj", filePath);
            print(pipingPrj, "pipingPrj", filePath);

            #region AutogenPropertyKeys for piping project == what is in AutogenProperty?
            StringCollection askeys;
            pipingPrj.GetAutogenPropertyKeys(true, out askeys);

            string autogenPropertyStr = getHeaderText("GetAutogenPropertyKeys");
            foreach (String sKey in askeys)
            {
                int nLastUsed, nIncrement;
                pipingPrj.GetProjectAutogenPropertyValue(sKey, out nLastUsed, out nIncrement);
                autogenPropertyStr += "\n" + sKey + ": " + "Last (" + nLastUsed + ") Increment (" + nIncrement + ")";
            }
            #endregion

            #region add/display project detail information
            ///add new category and property
            try
            {
                string catName = "ProjectDBInfo";
                string property1 = "SQLServerInstance";
                ProjectCategory newCat = new ProjectCategory(catName, "Information of Project DB");
                /////all four database must be wrote, so that it will be visiable in Project Setup
                ///add to pipingProject, the PnPProject under Piping.dcf
                addCategoryAndPropertyToProject(pipingPrj, newCat, property1,"Instance of Server", "LocalInstance");
                ///add to PnId project, the PnPProject table under ProcessPower.dcf
                addCategoryAndPropertyToProject(pnIdPrj, newCat, property1, "Instance of Server", "LocalInstance");
                ///add to Iso Project, the PnPProject table under Iso.dcf
                addCategoryAndPropertyToProject(isoPrj, newCat, property1, "Instance of Server", "LocalInstance");
                ///add to Ortho Project, the PnPProject table under Ortho.dcf
                addCategoryAndPropertyToProject(orthoPrj, newCat, property1, "Instance of Server", "LocalInstance");
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex) { ed.WriteMessage(ex.ToString()); }

            ///look through all project details
            int no = 1;
            List<ProjectCategory> projectCategories = pipingPrj.GetProjectCategoryMetadata();
            foreach (ProjectCategory projectCategory in projectCategories)
            {
                print(projectCategory, $"({no})projectCategory", filePath);
                List<ProjectProperty> projectProperties = pipingPrj.GetProjectPropertyMetadata(projectCategory.Name);
                int no2 = 1;
                foreach(ProjectProperty projectProperty in projectProperties)
                {
                    print(projectProperty, $"({no}.{no2})projectProperty", filePath);
                    File.AppendAllText(filePath, $"({no}.{no2}){projectProperty.Name} = {pipingPrj.GetProjectPropertyValue(projectProperty)}\n");

                    no2++;
                }
                no++;
            }
            #endregion
        }
        
        [CommandMethod("AddProperties")]
        public void AddProperties()
        {
            //file name to save different data 
            string filePath = @"C: \Users\LinP3\source\repos\Plant3DApiTester\Resource\plant_3d_AddProperties.txt";

            ///all available drawing type
            string DwgType_Piping = "Piping";
            string DwgType_PnId = "PnId";

            //get current drawing
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            ///create a clean file, prepare to write
            File.WriteAllText(filePath, $"{doc.Name}\n");

            //get current project
            PlantProject mainPrj = PlantApplication.CurrentProject;
            Project pipingPrj = mainPrj.ProjectParts[DwgType_Piping];
            DataLinksManager dlm = pipingPrj.DataLinksManager;

            //get current drawing type
            string dwgtype = PnPProjectUtils.GetActiveDocumentType();
            if (dwgtype != DwgType_PnId && dwgtype != DwgType_Piping)
            {  //"PnId", "Piping", "Ortho", "Iso"
                ed.WriteMessage("This drawing is not a P&ID or 3D Model in the current project.\n");
                return;
            }

            PnPDatabase pDB = dlm.GetPnPDatabase();
            PnPTable pTable = pDB.Tables["EngineeringItems"];
            string colName = "Api_added_Property1";
            bool isInTable = false;
            if (pTable != null)
            {
                ///list all columns in table
                PnPColumns columns = pTable.AllColumns;
                foreach (PnPColumn col in columns)
                {
                    File.AppendAllText(filePath, col.Name + Environment.NewLine);
                    if (colName == col.Name)
                        isInTable = true;
                }

                ///add new column
                try
                {
                   
                    if (isInTable == false)
                    {
                        PnPColumn newCol = new PnPColumn(colName, typeof(string), 256);
                        newCol.DefaultValue = "api default value";
                        pTable.Columns.Add(newCol);
                    }
                }
                catch (Autodesk.AutoCAD.Runtime.Exception ex) { ed.WriteMessage(ex.ToString()); }
            }


        }
        
        private void addCategoryAndPropertyToProject(Project prj, ProjectCategory newCat, string propName, string propDesc, string propValue)
        {
            ///add to pipingProject, the PnPProject under piping.dcf
            bool added = prj.AddProjectCategory(newCat);
            if (added)
            {
                ProjectProperty pp = new ProjectProperty(newCat.Name, propName, propDesc, propDesc);
                prj.AddProjectProperty(pp);
                ///above created a "DBInfo_DBInstance" column in PnPProject table under piping.dcf
                prj.SetProjectPropertyValue(pp, propValue);
            }
        }
        /// <summary>
        /// print all property of a object for inspection
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="Name"></param>
        /// <param name="filePath"></param>
        private void print(Object obj, string Name, string filePath)
        {
            string text = getHeaderText(Name);
            if (obj != null)
            {
                try
                {
                    foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(obj))
                    {
                        Document doc = AcadApp.DocumentManager.MdiActiveDocument;
                        Editor ed = doc.Editor;

                        string name = descriptor.Name;
                        object value = descriptor.GetValue(obj);
                        text += $"{name} = {value}\n";
                        //ed.WriteMessage(text);
                    }
                }
                catch { }
            }
            File.AppendAllText(filePath, text);
        }

        /// <summary>
        /// create a header string 
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        private string getHeaderText(string msg) => "======================================== " + msg + " ========================================\n";

        //The code sample below obtains the current project, if the active drawing is part of the project.
        public static Project PnPActiveProject()
        {
            Database oDatabase = AcadApp.DocumentManager.MdiActiveDocument.Database;
            PnIdProject oPrj = (PnIdProject)PlantApplication.CurrentProject.ProjectParts["PnId"];
            if (oPrj == null) return null; //No P&ID Project loaded
            List<PnPProjectDrawing> oDwgList = oPrj.GetPnPDrawingFiles();
            foreach (PnPProjectDrawing oDwg in oDwgList)
            {
                if (String.Equals(oDatabase.Filename, oDwg.ResolvedFilePath, StringComparison.InvariantCultureIgnoreCase))
                {
                    return oPrj;
                }
            }
                return null; //ActiveDocument is not in the CurrentProject
            
        }


    }
}

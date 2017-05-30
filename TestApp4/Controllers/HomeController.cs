using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Services;
using System.Web.Script.Services;
using TestApp4.Helpers;
using System.Web.Script.Serialization;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using Npgsql;
using NpgsqlTypes;
using TestApp4.Models;


namespace TestApp4.Controllers
{
    
    public class HomeController : Controller
    {

        
        public ActionResult JsTree()
        {
            List<ApplicationUser> getUsers = GetTestUsers();

            foreach (var item in getUsers)
            {
                Debug.WriteLine(item.UserName);
                List<string> userRoles = GetUserRoles(item.UserName);

                foreach (var role in userRoles)
                {
                    Debug.WriteLine(role);
                }
            }
            
            ViewBag.Message = "JsTree Demo";

            return View();
        }

        private List<ApplicationUser> GetTestUsers()
        {
            List<ApplicationUser> testUsers = new List<ApplicationUser>
            {
                new ApplicationUser
                {
                    Id = "1",
                    UserName = "Administrator"
                },
                new ApplicationUser
                {
                    Id = "2",
                    UserName = "Member"
                }
            };
            return testUsers;
        }

        public List<string> GetUserRoles(string user)
        {
            List<string> userRoles = new List<string>();
            switch (user)
            {
                case "Administrator":
                    userRoles = new List<string>(new string[] { "RoleAdmin" });
                    break;
                case "Member":
                    userRoles = new List<string>(new string[] { "RoleMember" });
                    break;
            }
            return userRoles;
        }

        public ActionResult LoadFiles(string folderid, string foldertext)
        {
            List<DisplayFiles> model = new List<DisplayFiles>();

            var setConnectionString = ConfigurationManager.ConnectionStrings["Postgres"];
            string getConnectionString = setConnectionString.ConnectionString;

            var npgsqlConnection = new NpgsqlConnection();
            npgsqlConnection.ConnectionString = getConnectionString;

            npgsqlConnection.Open();

            using (var command = new NpgsqlCommand())
            {
                command.Connection = npgsqlConnection;

                command.CommandText = "SELECT * FROM mrd_folder.upload WHERE folder_id = '" + folderid + "'";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        try
                        {
                            model.Add(
                                new DisplayFiles
                                {
                                    UploadId = (int)reader["upload_id"],
                                    FolderId = (string)reader["folder_id"],
                                    Filename = (string)reader["filename"],
                                    File = (Byte[])reader["myfile"]

                                }
                            );
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e.Message);
                            throw;
                        }
                    }
                }
                npgsqlConnection.Close();
            }
            return View(model);
        }

        private NpgsqlConnection SetConnection()
        {

            var setConnectionString = ConfigurationManager.ConnectionStrings["Postgres"];
            string getConnectionString = setConnectionString.ConnectionString;

            var npgsqlConnection = new NpgsqlConnection();
            npgsqlConnection.ConnectionString = getConnectionString;

            npgsqlConnection.Open();

            return npgsqlConnection;
        }

        public JsonResult GetJsonData()
        {
            var folders = new List<Folder>();
            var npgsqlConnection = SetConnection();

            using (var command = new NpgsqlCommand())
            {
                command.Connection = npgsqlConnection;
                command.CommandText = "SELECT folder_id, parent, jstreefolder.folder_name FROM mrd_folder.jstreefolder";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        try
                        {
                            folders.Add(
                                new Folder()
                                {
                                    id = ((reader["folder_id"] as string) ?? ""),
                                    //id = ((string)reader["folder_id"] == DBNull.Value.ToString()) ? string.Empty : (string)reader["folder_id"],
                                    parent = ((string)reader["parent"] == DBNull.Value.ToString()) ? string.Empty : (string)reader["parent"],
                                    text = (string)reader["folder_name"]
                                }
                            );
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e.Message);
                            throw;
                        }

                    }
                }
                npgsqlConnection.Close();
            }
            return Json(folders, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult HandleFileUploadDataBase(string myfolderId)
        {
            var npgsqlConnection = SetConnection();

            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = npgsqlConnection;
                var test = Request;
                for (int i = 0; i < Request.Files.Count; i++)
                {
                    int countup = i + 64;
                    cmd.CommandText = "INSERT INTO mrd_folder.upload (filename, myfile, folder_id) VALUES('" + Request.Files[i].FileName + "',:dataParam, '" + myfolderId + "') RETURNING upload_id";
                    //cmd.CommandText = "INSERT INTO mrd_folder.upload (filename, myfile) VALUES('"+Request.Files[i].FileName+"',:dataParam)";
                    NpgsqlParameter param = new NpgsqlParameter("dataParam", NpgsqlDbType.Bytea);

                    byte[] inputBytes;// = System.IO.File.ReadAllBytes(Request.Files[i].ToString());

                    using (var binaryReader = new BinaryReader(Request.Files[i].InputStream))
                    {
                        inputBytes = binaryReader.ReadBytes(Request.Files[i].ContentLength);
                    }

                    param.Value = inputBytes;
                    cmd.Parameters.Add(param);
                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception e)
                    {
                        Debug.Write(e.Message);
                        throw;
                    }

                    cmd.Parameters.Remove(param);

                }
            }
            npgsqlConnection.Close();
            
            return Json(null);
        }

        public ActionResult ReadFiles(int uploadId, string fileName)
        {
            string getExtension = Path.GetExtension(fileName);
            string contentType = string.Empty;
            int fName = Request.Files.Count;

            var npgsqlConnection = SetConnection();
            

            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = npgsqlConnection;

                cmd.CommandText = "SELECT myfile FROM mrd_folder.upload WHERE upload_id = '" + uploadId + "'";
                cmd.Parameters.AddWithValue("fileName", Request.QueryString["fileName"]);

                using (NpgsqlDataReader reader = cmd.ExecuteReader())
                {
                    byte[] chunk = new byte[10 * 1024];
                    if (reader.Read())
                    {
                        //byte[] chunk;
                        long read;
                        long offset = 0;

                        do
                        {
                            try
                            {
                                read = reader.GetBytes(0, offset, chunk, 0, chunk.Length);
                                if (read > 0)
                                {
                                    Response.OutputStream.Write(chunk, 0, (int)read);
                                    offset += read;
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.Write(e.Message);
                                throw;
                            }

                        }
                        while (read > 0);


                        npgsqlConnection.Close();

                        if (getExtension == ".docx")
                        {
                            Response.AddHeader("Content-Type", "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
                            //download file bzw. öffnet pdf nicht im browser
                            Response.AddHeader("Content-Disposition", "attachment; filename=" + fileName);
                        }
                        else if (getExtension == ".doc")
                        {
                            Response.AddHeader("Content-Type", "application/msword");
                            //download file bzw. öffnet pdf nicht im browser
                            Response.AddHeader("Content-Disposition", "attachment; filename=" + fileName);
                        }
                        else if (getExtension == ".pdf")
                        {
                            Response.AddHeader("Content-Type", "application/pdf");
                            //download file bzw. öffnet pdf nicht im browser
                            Response.AddHeader("Content-Disposition", "attachment; filename=" + fileName);
                        }
                        else if (getExtension == ".txt")
                        {
                            Response.AddHeader("Content-Type", "text/html");
                            //download file bzw. öffnet pdf nicht im browser
                            Response.AddHeader("Content-Disposition", "attachment; filename=" + fileName);
                        }
                        contentType = Response.ContentType;

                    }
                    return File(chunk, contentType);

                }

            }
            
        }

        public ActionResult DeleteDocument(int id)
        {
            var npgsqlConnection = SetConnection();

            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = npgsqlConnection;

                cmd.CommandText = "DELETE FROM mrd_folder.upload WHERE upload_id = '" + id + "'";
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    Debug.Write(e.Message);
                    throw;
                }
                finally
                {
                    npgsqlConnection.Close();
                }
            }
            //return null;
            return RedirectToAction("jstree");
            //return RedirectToAction("DisplayFiles");
        }

        [HttpPost]
        public ActionResult CreateFolder(string id, string foldername, int totalfolders)
        {
            var npgsqlConnection = SetConnection();
            bool parent = true;
            string setParent = String.Empty;
            string[] trimId;
            Char delimiter = '_';

            if (id.IndexOf("_") >= 0) { parent = false; }

            if (parent)
            {
                id = (totalfolders + 1).ToString();
                setParent = "#";
            }
            else
            {
                trimId = id.Split('_');
            }

            using (var cmd = new NpgsqlCommand())
            {
                try
                {
                    cmd.Connection = npgsqlConnection;

                    cmd.CommandText = "INSERT INTO mrd_folder.jstreefolder(folder_id, parent, folder_name) VALUES ('" + id + "', '" + setParent + "', '" + foldername + "')";
                    cmd.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    throw;
                }
                finally
                {
                    npgsqlConnection.Close();
                }
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public ActionResult CreateSubFolder(string id, string foldername, string parent)
        {
            return View();
        }

        [HttpPost]
        public ActionResult DeleteFolder(string foldername)
        {
            var npgsqlConnection = SetConnection();

            using (var cmd = new NpgsqlCommand())
            {
                try
                {
                    cmd.Connection = npgsqlConnection;

                    cmd.CommandText = "DELETE FROM mrd_folder.jstreefolder WHERE folder_name = '" + foldername + "'";
                    cmd.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    Debug.Write(e.Message);
                    throw;
                }
                finally
                {
                    npgsqlConnection.Close();
                }



            }
            return RedirectToAction("Index");
        }

        [HttpPost]

        public ActionResult EditFolder(string foldernameold, string foldernamenew)
        {
            var npgsqlConnection = SetConnection();

            try
            {
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = npgsqlConnection;

                    cmd.CommandText = "UPDATE mrd_folder.jstreefolder SET folder_name = '" + foldernamenew + "' WHERE folder_name = '" + foldernameold + "'";
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                Debug.Write(e.Message);
                throw;
            }
            finally
            {
                npgsqlConnection.Close();
            }

            return RedirectToAction("Index");
        }

        public class Folder
        {
            public string id { get; set; }
            public string parent { get; set; }
            public string text { get; set; }
            
        }

        public class DisplayFiles
        {
            public int UploadId { get; set; }
            public string Filename { get; set; }
            public string FolderId { get; set; }
            public byte[] File { get; set; }
        }

        public ActionResult GetAllNodes()
        {
            //string str = "[{\"id\":1,\"text\":\"Root node\",\"children\":[{\"id\":2,\"text\":\"Child node 1\",\"children\":true},{\"id\":3,\"text\":\"Child node 2\"}]}]";


            List<string> list = new List<string>();
            string a = "a";
            list.Add(a);
            string b = "c";
            list.Add(b);
            string c = "b";
            list.Add(c);



            return RedirectToAction("JsTree");
        }

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        //enum IconSize
        //{
        //    Small,
        //    Large
        //}

        //public static System.Drawing.Icon GetFileIcon(string name, IconSize size, bool linkOverlay)
        //{
        //    Shell32.SHFILEINFO shfi = new Shell32.SHFILEINFO();
        //    uint flags = Shell32.SHGFI_ICON | Shell32.SHGFI_USEFILEATTRIBUTES;

        //    if (true == linkOverlay) flags += Shell32.SHGFI_LINKOVERLAY;

        //    /* size /*
        //     * 
        //     * */

        //    if(IconSize.Small = )

        //}


    }
}
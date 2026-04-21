using RyC;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;

namespace RyC.Controllers
{
    [RoutePrefix("api/users")]
    public class UsersController : ApiController
    {
        private readonly RYCEntities db = new RYCEntities();

        // GET: api/users/Currentuser?uid=1
        [HttpGet]
        [Route("Currentuser")]
        public HttpResponseMessage GetCurrentUser(int uid)
        {
            try
            {
                if (uid <= 0) return Request.CreateResponse(HttpStatusCode.BadRequest, "Valid user id required.");

                var user = db.Users.FirstOrDefault(u => u.uid == uid);
                if (user == null) return Request.CreateResponse(HttpStatusCode.NotFound, "User not found.");

                string expertCategory = "Expert";
                if (user.type == "expert")
                {
                    var expertRec = db.Experts.FirstOrDefault(e => e.uid == uid);
                    if (expertRec != null && !string.IsNullOrWhiteSpace(expertRec.category))
                    {
                        expertCategory = expertRec.category;
                    }
                }

                // 👇 CLEAN URL LOGIC ADDED: Dashboard k liye picture bhi bhejein
                string cleanPicturePath = user.upicture != null ? user.upicture.Replace("~", "") : "";
                if (!string.IsNullOrEmpty(cleanPicturePath) && !cleanPicturePath.StartsWith("/"))
                {
                    cleanPicturePath = "/" + cleanPicturePath;
                }

                var responseBody = new
                {
                    userId = user.uid,
                    username = user.username,
                    type = user.type,
                    category = expertCategory,
                    upicture = cleanPicturePath
                };

                return Request.CreateResponse(HttpStatusCode.OK, responseBody);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        // GET: api/users/getallusers
        [HttpGet]
        [Route("getallusers")]
        public HttpResponseMessage GetAllUsers()
        {
            try
            {
                // Sirf "user" type walay log fetch karega (admin ya expert nahi)
                var users = db.Users
                              .Where(u => u.type == "user")
                              .Select(u => new {
                                  uid = u.uid,
                                  username = u.username
                              })
                              .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, users);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }



        // POST: api/users/{uid}/default-vehicle
        [HttpPost]
        [Route("{uid:int}/default-vehicle")]
        public HttpResponseMessage SetDefaultVehicle(int uid, [FromBody] int vid)
        {
            try
            {
                if (uid <= 0 || vid <= 0)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Valid user id (uid) and vehicle id (vid) required."
                    );
                }

                var user = db.Users.FirstOrDefault(u => u.uid == uid);
                if (user == null)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "User not found."
                    );
                }

                if (!string.Equals(user.type, "user", StringComparison.OrdinalIgnoreCase))
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Default vehicle can only be set for a user."
                    );
                }

                var vehicle = db.Vehicles.FirstOrDefault(v => v.vid == vid);
                if (vehicle == null)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "Vehicle not found."
                    );
                }

                user.default_vid = vid;
                db.SaveChanges();

                var responseBody = new
                {
                    message = "Default vehicle set successfully.",
                    userId = user.uid,
                    defaultVehicle = new
                    {
                        vid = vehicle.vid,
                        vehicle.make,
                        vehicle.model,
                        vehicle.variant,
                        vehicle.year
                    }
                };

                return Request.CreateResponse(
                    HttpStatusCode.OK,
                    responseBody
                );
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(
                    HttpStatusCode.InternalServerError,
                    "Something went wrong: " + ex.Message
                );
            }
        }

        // 1. Profile Data Get Karein (Including Picture & Category)
        // GET: api/users/getprofile?uid=1
        [HttpGet]
        [Route("getprofile")]
        public HttpResponseMessage GetProfile(int uid)
        {
            try
            {
                var user = db.Users.FirstOrDefault(u => u.uid == uid);
                if (user == null) return Request.CreateResponse(HttpStatusCode.NotFound, "User not found.");

                // Expert table se category (Expertise) uthain
                string category = "Electrical";
                if (user.type.ToLower() == "expert")
                {
                    var expert = db.Experts.FirstOrDefault(e => e.uid == uid);
                    if (expert != null) category = expert.category;
                }

                // 👇 CLEAN URL LOGIC ADDED: Tilde (~) ko hata dega
                string cleanPicturePath = user.upicture != null ? user.upicture.Replace("~", "") : "";
                if (!string.IsNullOrEmpty(cleanPicturePath) && !cleanPicturePath.StartsWith("/"))
                {
                    cleanPicturePath = "/" + cleanPicturePath;
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    uid = user.uid,
                    username = user.username,
                    upicture = cleanPicturePath,
                    type = user.type,
                    category = category,
                    default_vid = user.default_vid
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [Route("updateexpertprofile")]
        public HttpResponseMessage UpdateExpertProfile()
        {
            try
            {
                var httpRequest = HttpContext.Current.Request;

                // Form Data read karein
                int uid = int.Parse(httpRequest.Form["uid"]);
                string username = httpRequest.Form["username"];
                string oldPassword = httpRequest.Form["oldpassword"];
                string newPassword = httpRequest.Form["newpassword"];
                string category = httpRequest.Form["category"];

                var user = db.Users.FirstOrDefault(u => u.uid == uid);
                if (user == null) return Request.CreateResponse(HttpStatusCode.NotFound, "User not found.");

                // 1. Password Verification 
                if (!string.IsNullOrWhiteSpace(newPassword))
                {
                    if (string.IsNullOrWhiteSpace(oldPassword) || user.password != oldPassword)
                    {
                        return Request.CreateResponse(HttpStatusCode.BadRequest, "Old password is incorrect!");
                    }
                    user.password = newPassword;
                }

                // 2. Username Update
                user.username = username;

                // 3. Image Upload handle karein 
                if (httpRequest.Files.Count > 0)
                {
                    var postedFile = httpRequest.Files[0];
                    string folderPath = HttpContext.Current.Server.MapPath("~/ProfileImages/");

                    if (!System.IO.Directory.Exists(folderPath))
                        System.IO.Directory.CreateDirectory(folderPath);

                    string fileName = uid + "_" + DateTime.Now.Ticks + ".jpg";
                    string filePath = System.IO.Path.Combine(folderPath, fileName);

                    postedFile.SaveAs(filePath);

                    user.upicture = "/ProfileImages/" + fileName;
                }

                // 4. Expert Table mein Expertise badlain
                var expert = db.Experts.FirstOrDefault(e => e.uid == uid);
                if (expert != null)
                {
                    expert.category = category;
                }

                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    message = "Profile updated successfully!",
                    upicture = user.upicture
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "Error: " + ex.Message);
            }
        }

        // PUT: api/users/{uid}
        [HttpPut]
        [Route("{uid:int}")]
        public HttpResponseMessage UpdateUser(int uid, [FromBody] User model)
        {
            try
            {
                if (uid <= 0)
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Valid user id required.");

                var user = db.Users.FirstOrDefault(u => u.uid == uid);
                if (user == null)
                    return Request.CreateResponse(HttpStatusCode.NotFound, "User not found.");

                if (model == null || string.IsNullOrWhiteSpace(model.username))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        "Username is required."
                    );
                }

                user.username = model.username.Trim();

                if (!string.IsNullOrWhiteSpace(model.password))
                {
                    user.password = model.password;
                }

                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    message = "User updated successfully.",
                    userId = user.uid,
                    user.username,
                    user.type
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(
                    HttpStatusCode.InternalServerError,
                    "Something went wrong: " + ex.Message
                );
            }
        }

        // DELETE: api/users/{uid} (Yeh aapka apna original safe wala delete hai)
        [HttpDelete]
        [Route("{uid:int}")]
        public HttpResponseMessage DeleteUser(int uid)
        {
            try
            {
                if (uid <= 0)
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        "Valid user id required.");

                var user = db.Users.FirstOrDefault(u => u.uid == uid);
                if (user == null)
                    return Request.CreateResponse(HttpStatusCode.NotFound,
                        "User not found.");

                if (string.Equals(user.type, "admin", StringComparison.OrdinalIgnoreCase))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        "Admin user cannot be deleted.");
                }

                var ratings = db.UserRatings.Where(r => r.uid == uid).ToList();
                db.UserRatings.RemoveRange(ratings);

                var expert = db.Experts.FirstOrDefault(e => e.uid == uid);
                if (expert != null)
                {
                    var expertLinks = db.ExpertSolutions
                                        .Where(es => es.eid == expert.eid)
                                        .ToList();
                    db.ExpertSolutions.RemoveRange(expertLinks);

                    db.Experts.Remove(expert);
                }

                db.Users.Remove(user);
                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    message = "User deleted successfully.",
                    deletedUserId = uid
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(
                    HttpStatusCode.InternalServerError,
                    "Something went wrong: " + ex.Message
                );
            }
        }
        // =======================================================
        // 👇 NAYI API: Admin ke "Add User" button ke liye 👇
        // =======================================================
        [HttpPost]
        [Route("add")]
        public HttpResponseMessage AddUser([FromBody] User model)
        {
            try
            {
                // 1. Check if fields are empty
                if (model == null || string.IsNullOrWhiteSpace(model.username) || string.IsNullOrWhiteSpace(model.password))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Username and password are required.");
                }

                // 2. Check if username already exists (Duplicate Check)
                bool exists = db.Users.Any(u => u.username.ToLower() == model.username.ToLower());
                if (exists)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Username already exists. Please choose a different one.");
                }

                // 3. Save new user to Database
                var newUser = new User
                {
                    username = model.username.Trim(),
                    password = model.password,
                    type = "user" // Hamesha standard user banayega
                };

                db.Users.Add(newUser);
                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, "User added successfully!");
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }
        // =======================================================
        // 👇 EXPERT MANAGEMENT APIs (Admin Dashboard) 👇
        // =======================================================

        [HttpGet]
        [Route("getapprovedexperts")]
        public HttpResponseMessage GetApprovedExperts()
        {
            try
            {
                // Jo DB mein 'expert' hain, unhe fetch karo
                var experts = db.Users.Where(u => u.type == "expert")
                                .Select(u => new { u.uid, u.username, u.upicture }).ToList();
                return Request.CreateResponse(HttpStatusCode.OK, experts);
            }
            catch (Exception ex) { return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message); }
        }

        [HttpGet]
        [Route("getpendingexperts")]
        public HttpResponseMessage GetPendingExperts()
        {
            try
            {
                // Jo DB mein 'pending' hain, unhe fetch karo
                var pending = db.Users.Where(u => u.type == "pending")
                                .Select(u => new { u.uid, u.username, u.upicture }).ToList();
                return Request.CreateResponse(HttpStatusCode.OK, pending);
            }
            catch (Exception ex) { return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message); }
        }

        [HttpPut]
        [Route("approveexpert/{uid:int}")]
        public HttpResponseMessage ApproveExpert(int uid)
        {
            try
            {
                var user = db.Users.FirstOrDefault(u => u.uid == uid);
                if (user == null) return Request.CreateResponse(HttpStatusCode.NotFound, "User not found.");

                // Status 'pending' se 'expert' kar diya
                user.type = "expert";
                db.SaveChanges();
                return Request.CreateResponse(HttpStatusCode.OK, "Expert Approved Successfully!");
            }
            catch (Exception ex) { return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message); }
        }

        [HttpPost]
        [Route("addexpertadmin")]
        public HttpResponseMessage AddExpertAdmin([FromBody] dynamic data)
        {
            try
            {
                string username = data.username;
                string password = data.password;
                string category = data.category;

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Fill all fields.");

                if (db.Users.Any(u => u.username.ToLower() == username.ToLower()))
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Username already exists.");

                // Pehle User Table mein daalo
                var newUser = new User { username = username.Trim(), password = password, type = "expert" };
                db.Users.Add(newUser);
                db.SaveChanges(); // UID generate hone ke liye Save zaroori hai

                // Phir Expert Table mein daalo
                var newExpert = new Expert { uid = newUser.uid, category = category };
                db.Experts.Add(newExpert);
                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, "Expert Added Successfully!");
            }
            catch (Exception ex) { return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message); }
        }

        // =======================================================
        // 👇 Get All Vehicles of a Specific User (UPDATED WITH FALLBACK) 👇
        // =======================================================
        [HttpGet]
        [Route("{uid:int}/vehicles")]
        public HttpResponseMessage GetUserVehicles(int uid)
        {
            try
            {
                if (uid <= 0)
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Valid user id required.");

                // 1. Check if user exists
                var user = db.Users.FirstOrDefault(u => u.uid == uid);
                if (user == null)
                    return Request.CreateResponse(HttpStatusCode.NotFound, "User not found.");

                // 2. Naye Junction table (UserVehicles) se gaariyan nikalo
                var userVehiclesList = (from uv in db.UserVehicles
                                        join v in db.Vehicles on uv.vid equals v.vid
                                        where uv.uid == uid
                                        select new
                                        {
                                            vid = v.vid,
                                            make = v.make,
                                            model = v.model,
                                            variant = v.variant,
                                            year = v.year
                                        }).ToList();

                // 3. 👇 SMART LOGIC: Agar user ki default_vid set hai, aur wo list mein nahi aayi, toh usko bhi shamil karo!
                if (user.default_vid.HasValue)
                {
                    bool alreadyInList = userVehiclesList.Any(v => v.vid == user.default_vid.Value);
                    if (!alreadyInList)
                    {
                        var defaultCar = db.Vehicles.FirstOrDefault(v => v.vid == user.default_vid.Value);
                        if (defaultCar != null)
                        {
                            // Default gaari ko list mein sab se upar (index 0) par daal do
                            userVehiclesList.Insert(0, new
                            {
                                vid = defaultCar.vid,
                                make = defaultCar.make,
                                model = defaultCar.model,
                                variant = defaultCar.variant,
                                year = defaultCar.year
                            });
                        }
                    }
                }

                // 4. Response bhejo
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    message = "Vehicles fetched successfully.",
                    total = userVehiclesList.Count,
                    vehicles = userVehiclesList
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "Error: " + ex.Message);
            }
        }

        // =======================================================
        // 👇 Add Vehicle to User's Garage (Choose Car) 👇
        // =======================================================
        public class AddUserVehicleModel
        {
            public int vid { get; set; }
            public bool isDefault { get; set; }
        }

        [HttpPost]
        [Route("{uid:int}/vehicles")]
        public HttpResponseMessage AddUserVehicle(int uid, [FromBody] AddUserVehicleModel data)
        {
            try
            {
                if (uid <= 0 || data == null || data.vid <= 0)
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Invalid data.");

                var user = db.Users.FirstOrDefault(u => u.uid == uid);
                if (user == null)
                    return Request.CreateResponse(HttpStatusCode.NotFound, "User not found.");

                // 1. Check karein ke gaari pehle se toh nahi hai
                var existingCar = db.UserVehicles.FirstOrDefault(uv => uv.uid == uid && uv.vid == data.vid);
                if (existingCar == null)
                {
                    // Naye table mein gaari daal do (Singular 'UserVehicle' use kiya hai)
                    var newUv = new UserVehicle
                    {
                        uid = uid,
                        vid = data.vid,
                        status = "active"
                    };
                    db.UserVehicles.Add(newUv);
                }

                // 2. Agar user ne 'Default' par tick kiya hai, toh uski profile mein bhi save karo
                if (data.isDefault)
                {
                    user.default_vid = data.vid;
                }

                db.SaveChanges();
                return Request.CreateResponse(HttpStatusCode.OK, "Car added successfully!");
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "Error: " + ex.Message);
            }
        }
    }
}
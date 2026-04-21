using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using RyC.Models;
using RYC;

namespace RyC.Controllers
{
    [RoutePrefix("api/auth")]
    public class AuthController : ApiController
    {
        private readonly RYCEntities db = new RYCEntities();

        [HttpPost]
        [Route("register")]
        public HttpResponseMessage Register([FromBody] RegisterRequestModel model)
        {
            try
            {
                if (model == null ||
                    string.IsNullOrWhiteSpace(model.username) ||
                    string.IsNullOrWhiteSpace(model.password) ||
                    string.IsNullOrWhiteSpace(model.type))
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Username, Password, Type required."
                    );
                }

                var userType = model.type.ToLower();
                var allowedTypes = new[] { "user", "expert", "admin" };

                if (!allowedTypes.Contains(userType))
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Type must be 'user', 'expert' or 'admin'."
                    );
                }

                var existingUser = db.Users
                    .FirstOrDefault(u => u.username == model.username);

                if (existingUser != null)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.Conflict,
                        "Username already taken."
                    );
                }

                // 👇 1. Pehle User table mein data dala
                var user = new User
                {
                    username = model.username.Trim(),
                    password = model.password,
                    // FIX: Agar expert hai to database mein 'pending' save karo
                    type = (userType == "expert") ? "pending" : userType
                };

                db.Users.Add(user);
                db.SaveChanges(); // Save hone par database khud isko ek 'uid' de dega

                // 👇 2. Ab Expert table mein data dala
                // FIX: Yahan 'user.type' ki jagah original 'userType' variable use kar rahay hain
                if (userType == "expert")
                {
                    var expert = new Expert
                    {
                        uid = user.uid, // Jo naya user bana, uska uid pakra
                        category = string.IsNullOrWhiteSpace(model.category) ? "Mechanical" : model.category
                    };

                    db.Experts.Add(expert);
                    db.SaveChanges();
                }

                if (user.type == "admin" || userType == "admin")
                {
                    var admin = new Admin
                    {
                        uid = user.uid,
                        joining_date = DateTime.Today
                    };

                    db.Admins.Add(admin);
                    db.SaveChanges();
                }

                return Request.CreateResponse(HttpStatusCode.Created, new
                {
                    message = "User registered successfully.",
                    userId = user.uid,
                    username = user.username,
                    type = user.type // Yahan frontend ko automatically updated type return hogi
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

        // ================= LOGIN =================
        [HttpPost]
        [Route("login")]
        public HttpResponseMessage Login([FromBody] User model)
        {
            try
            {
                if (model == null ||
                    string.IsNullOrWhiteSpace(model.username) ||
                    string.IsNullOrWhiteSpace(model.password))
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Username and Password required."
                    );
                }

                var user = db.Users.FirstOrDefault(u =>
                    u.username == model.username &&
                    u.password == model.password);

                if (user == null)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.Unauthorized,
                        "Invalid credentials."
                    );
                }

                object defaultVehicle = null;
                int? expertId = null;

                // Agar User hai, to gaari nikalo
                if (user.type == "user" && user.default_vid.HasValue)
                {
                    defaultVehicle = db.Vehicles
                        .Where(v => v.vid == user.default_vid.Value)
                        .Select(v => new
                        {
                            vid = v.vid,
                            make = v.make,
                            model = v.model,
                            variant = v.variant,
                            year = v.year
                        })
                        .FirstOrDefault();
                }
                // Agar Expert hai, to uska eid nikalo
                else if (user.type == "expert")
                {
                    var expertRecord = db.Experts.FirstOrDefault(e => e.uid == user.uid);
                    if (expertRecord != null)
                    {
                        expertId = expertRecord.eid;
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    message = "Login successful.",
                    userId = user.uid,
                    username = user.username,
                    type = user.type,
                    defaultVehicle = defaultVehicle,
                    expertId = expertId
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
    }
}
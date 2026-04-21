using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.IO;
using RyC;

namespace RyC.Controllers
{
    [RoutePrefix("api/vehicles")]
    public class VehiclesController : ApiController
    {
        private readonly RYCEntities db = new RYCEntities();

        // GET: api/vehicles/getallvehicles
        [HttpGet]
        [Route("getallvehicles")]
        public HttpResponseMessage GetAllVehicles()
        {
            try
            {
                var data = db.Vehicles
                    .Select(v => new
                    {
                        v.vid,
                        v.make,
                        v.model,
                        v.variant,
                        v.year
                    })
                    .OrderBy(v => v.make)
                    .ThenBy(v => v.model)
                    .ToList();

                return Request.CreateResponse(
                    HttpStatusCode.OK,
                    new
                    {
                        message = "All vehicles fetched successfully.",
                        total = data.Count,
                        vehicles = data
                    }
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

        // GET: api/vehicles/make
        [HttpGet]
        [Route("make")]
        public HttpResponseMessage GetMakes()
        {
            try
            {
                List<string> makes = db.Vehicles
                    .Select(v => v.make)
                    .Distinct() //unique
                    .OrderBy(m => m)
                    .ToList();

                var responseBody = new
                {
                    message = "Vehicle makes fetched successfully.",
                    total = makes.Count,
                    makes = makes
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

        // GET: api/vehicles/models?make=Suzuki
        [HttpGet]
        [Route("models")]
        public HttpResponseMessage GetModels(string make)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(make))
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Make parameter required."
                    );
                }

                var models = db.Vehicles
                    .Where(v => v.make == make)
                    .Select(v => v.model)
                    .Distinct()
                    .OrderBy(m => m)
                    .ToList();

                var responseBody = new
                {
                    message = "Vehicle models fetched successfully.",
                    total = models.Count,
                    make = make,
                    models = models
                };

                return Request.CreateResponse(HttpStatusCode.OK, responseBody);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(
                    HttpStatusCode.InternalServerError,
                    "Something went wrong: " + ex.Message
                );
            }
        }

        // GET: api/vehicles/variants?make=Suzuki&model=Mehran
        [HttpGet]
        [Route("variants")]
        public HttpResponseMessage GetVariants(string make, string model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(make) || string.IsNullOrWhiteSpace(model))
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Make and Model required."
                    );
                }

                var variants = db.Vehicles
                    .Where(v => v.make == make && v.model == model)
                    .Select(v => v.variant)
                    .Distinct()
                    .OrderBy(v => v)
                    .ToList();

                var responseBody = new
                {
                    message = "Vehicle variants fetched successfully.",
                    make = make,
                    model = model,
                    total = variants.Count,
                    variants = variants
                };

                return Request.CreateResponse(HttpStatusCode.OK, responseBody);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(
                    HttpStatusCode.InternalServerError,
                    "Something went wrong: " + ex.Message
                );
            }
        }

        // GET: api/vehicles/{vid}/problems
        [HttpGet]
        [Route("{vid:int}/problems")]
        public HttpResponseMessage GetProblemsForVehicle(int vid)
        {
            try
            {
                if (vid <= 0)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Valid vehicle id (vid) required."
                    );
                }

                var vehicleExists = db.Vehicles.Any(v => v.vid == vid);
                if (!vehicleExists)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "Vehicle Problem not found."
                    );
                }

                var problems = db.VehicleProblemSolutions
                    .Where(x => x.vid == vid)
                    .Select(x => x.Problem)
                    .Distinct()
                    .Select(p => new
                    {
                        pid = p.pid,
                        title = p.ptitle,
                        description = p.pdescription,
                        problemType = p.ptype,   // electrical / mechanical
                        addedByType = p.type,    // admin / expert 
                        addedByUserId = p.uid
                    })
                    .ToList();

                var responseBody = new
                {
                    message = "Problems fetched successfully.",
                    vehicleId = vid,
                    total = problems.Count,
                    problems = problems
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

        // POST: api/vehicles/addvehicle
        [HttpPost]
        [Route("addvehicle")]
        public HttpResponseMessage AddVehicle([FromBody] Vehicle v)
        {
            try
            {
                if (v == null)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Vehicle data required."
                    );
                }

                if (string.IsNullOrWhiteSpace(v.make) ||
                    string.IsNullOrWhiteSpace(v.model) ||
                    string.IsNullOrWhiteSpace(v.variant) ||
                    v.year <= 0)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "make, model, variant and year required."
                    );
                }

                v.make = v.make.Trim();
                v.model = v.model.Trim();
                v.variant = v.variant.Trim();

                var exists = db.Vehicles.Any(x =>
                    x.make == v.make &&
                    x.model == v.model &&
                    x.variant == v.variant &&
                    x.year == v.year
                );

                if (exists)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.Conflict,   // 409
                        "vehicle already exist."
                    );
                }

                db.Vehicles.Add(v);
                db.SaveChanges();

                var responseBody = new
                {
                    message = "Vehicle added successfully.",
                    vid = v.vid,
                    make = v.make,
                    model = v.model,
                    variant = v.variant,
                    year = v.year
                };

                return Request.CreateResponse(
                    HttpStatusCode.Created,     // 201
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

        // ======================================================
        // 👇 YAHAN RATING AUR REVIEWS KI LOGIC THEEK KI GAYI HAI 👇
        // ======================================================
        // GET: api/vehicles/{vid}/problems/{pid}/solutions
        [HttpGet]
        [Route("{vid:int}/problems/{pid:int}/solutions")]
        public HttpResponseMessage GetSolutionsForVehicleProblem(int vid, int pid)
        {
            try
            {
                if (vid <= 0 || pid <= 0)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Valid vehicle id (vid) aur problem id (pid) required hain.");
                }

                var vehicleExists = db.Vehicles.Any(v => v.vid == vid);
                if (!vehicleExists) return Request.CreateResponse(HttpStatusCode.NotFound, "Vehicle not found.");

                var problemExists = db.Problems.Any(p => p.pid == pid);
                if (!problemExists) return Request.CreateResponse(HttpStatusCode.NotFound, "Problem not found.");

                var solutions = db.VehicleProblemSolutions
                    .Where(vps => vps.vid == vid && vps.pid == pid)
                    .Select(vps => new
                    {
                        solutionId = vps.Solution.sid,
                        title = vps.Solution.stitle,
                        date = vps.Solution.date,
                        expertId = vps.eid,
                        expertName = vps.Expert.User.username,
                        // 👇 CLEAN URL LOGIC ADDED: Expert ki picture bhi bhejein (Tilde hata kar) 👇
                        expertPicture = vps.Expert.User.upicture != null ? vps.Expert.User.upicture.Replace("~", "") : "",
                        
                        // 👇 SMART RATING CALCULATION (Sirf > 0 wali ratings) 👇
                        reviewCount = db.UserRatings.Count(r => r.sid == vps.Solution.sid && r.rating > 0),
                        overallRating = db.UserRatings.Where(r => r.sid == vps.Solution.sid && r.rating > 0)
                                                      .Average(r => (double?)r.rating) ?? 0.0,

                        steps = vps.Solution.Steps
                            .OrderBy(st => st.stepNo)
                            .Select(st => new
                            {
                                stepId = st.stepid,
                                stepNo = st.stepNo,
                                stepDescription = st.stepDescription,
                                stepImg = st.stepImg
                            }).ToList()
                    })
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    message = "Solutions fetched successfully.",
                    vehicleId = vid,
                    problemId = pid,
                    total = solutions.Count,
                    solutions = solutions
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "Something went wrong: " + ex.Message);
            }
        }


        // POST: api/vehicles/{vid}/problems/{pid}/solutions?eid=3  
        [HttpPost]
        [Route("{vid:int}/problems/{pid:int}/solutions")]
        public HttpResponseMessage AddSolutionForVehicleProblem(int vid, int pid, int eid, [FromBody] Solution model)
        {
            try
            {
                if (vid <= 0 || pid <= 0 || eid <= 0)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Valid vehicle id (vid), problem id (pid) and expert id (eid) required."
                    );
                }

                if (model == null || string.IsNullOrWhiteSpace(model.stitle))
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Solution title (stitle) required."
                    );
                }

                var vehicleExists = db.Vehicles.Any(v => v.vid == vid);
                if (!vehicleExists)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "Vehicle not found."
                    );
                }

                var problemExists = db.Problems.Any(p => p.pid == pid);
                if (!problemExists)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "Problem not found."
                    );
                }

                var expertExists = db.Experts.Any(e => e.eid == eid);
                if (!expertExists)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "Expert not found."
                    );
                }

                var solution = new Solution
                {
                    stitle = model.stitle,
                    date = DateTime.Now
                };

                db.Solutions.Add(solution);
                db.SaveChanges();

                var vps = new VehicleProblemSolution
                {
                    vid = vid,
                    pid = pid,
                    sid = solution.sid,
                    eid = eid,
                    overallrating = 0
                };

                db.VehicleProblemSolutions.Add(vps);
                db.SaveChanges();

                var responseBody = new
                {
                    message = "Solution added and linked successfully.",
                    vehicleId = vid,
                    problemId = pid,
                    expertId = eid,
                    solutionId = solution.sid,
                    title = solution.stitle,
                    date = solution.date,
                    overallRating = vps.overallrating
                };

                return Request.CreateResponse(
                    HttpStatusCode.Created,
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
        [HttpPut]
        [Route("{vid:int}")]
        public HttpResponseMessage UpdateVehicle(int vid, [FromBody] Vehicle modelData)
        {
            try
            {
                if (vid <= 0)
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        "Valid vehicle id required.");

                var vehicle = db.Vehicles.FirstOrDefault(v => v.vid == vid);
                if (vehicle == null)
                    return Request.CreateResponse(HttpStatusCode.NotFound,
                        "Vehicle not found.");

                if (modelData == null ||
                    string.IsNullOrWhiteSpace(modelData.make) ||
                    string.IsNullOrWhiteSpace(modelData.model))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        "Make and model required.");
                }

                // Duplicate check
                bool exists = db.Vehicles.Any(v =>
                    v.make == modelData.make &&
                    v.model == modelData.model &&
                    v.variant == modelData.variant &&
                    v.year == modelData.year &&
                    v.vid != vid);

                if (exists)
                {
                    return Request.CreateResponse(HttpStatusCode.Conflict,
                        "Vehicle already exists.");
                }

                vehicle.make = modelData.make.Trim();
                vehicle.model = modelData.model.Trim();
                vehicle.variant = modelData.variant;
                vehicle.year = modelData.year;

                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    message = "Vehicle updated successfully.",
                    vehicleId = vehicle.vid,
                    vehicle.make,
                    vehicle.model,
                    vehicle.variant,
                    vehicle.year
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(
                    HttpStatusCode.InternalServerError,
                    "Something went wrong: " + ex.Message);
            }
        }
        // ======================================================
        // DELETE: api/vehicles/{vid}
        // Safe delete vehicle
        // ======================================================
        [HttpDelete]
        [Route("{vid:int}")]
        public HttpResponseMessage DeleteVehicle(int vid)
        {
            try
            {
                if (vid <= 0)
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        "Valid vehicle id required.");

                var vehicle = db.Vehicles.FirstOrDefault(v => v.vid == vid);
                if (vehicle == null)
                    return Request.CreateResponse(HttpStatusCode.NotFound,
                        "Vehicle not found.");

                // Check VehicleProblemSolution links
                bool isLinkedVPS = db.VehicleProblemSolutions
                                     .Any(v => v.vid == vid);

                if (isLinkedVPS)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        "Cannot delete vehicle. Linked with problem solutions.");
                }

                // Check Users default vehicle
                bool isDefaultVehicle = db.Users
                                          .Any(u => u.default_vid == vid);

                if (isDefaultVehicle)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        "Cannot delete vehicle. It is set as default vehicle for some users.");
                }

                db.Vehicles.Remove(vehicle);
                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    message = "Vehicle deleted successfully.",
                    deletedVehicleId = vid
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(
                    HttpStatusCode.InternalServerError,
                    "Something went wrong: " + ex.Message);
            }
        }

        // ======================================================
        // GET: api/vehicles/pending
        // Sirf un gariyon ko laaye jinka status 'pending' hai
        // ======================================================
        [HttpGet]
        [Route("pending")]
        public HttpResponseMessage GetPendingVehicles()
        {
            try
            {
                var data = db.Vehicles
                    .Where(v => v.status == "Pending")
                    .Select(v => new { v.vid, v.make, v.model, v.variant, v.year })
                    .OrderByDescending(v => v.vid)
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, new { message = "Pending vehicles fetched", vehicles = data });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        // ======================================================
        // GET: api/vehicles/approve?vid=5
        // Expert ki bheji hui gari ko approve kare
        // ======================================================
        [HttpGet]
        [Route("approve")]
        public HttpResponseMessage ApproveVehicle(int vid)
        {
            try
            {
                var vehicle = db.Vehicles.FirstOrDefault(v => v.vid == vid);
                if (vehicle == null) return Request.CreateResponse(HttpStatusCode.NotFound, "Vehicle not found");

                vehicle.status = "Approved";
                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, new { message = "Vehicle Approved" });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace RyC.Controllers
{
    [RoutePrefix("api/solutions")]
    public class SolutionsController : ApiController
    {
        private readonly RYCEntities db = new RYCEntities();

        // ======================================================
        // GET: api/solutions/{sid}
        // ======================================================
        [HttpGet]
        [Route("{sid:int}")]
        public HttpResponseMessage GetSolutionById(int sid)
        {
            try
            {
                if (sid <= 0) return Request.CreateResponse(HttpStatusCode.BadRequest, "Valid solution id required.");

                var solution = db.Solutions.Where(s => s.sid == sid).Select(s => new { sid = s.sid, title = s.stitle, date = s.date }).FirstOrDefault();
                if (solution == null) return Request.CreateResponse(HttpStatusCode.NotFound, "Solution not found.");

                var steps = db.Steps.Where(st => st.sid == sid).OrderBy(st => st.stepNo)
                    .Select(st => new { stepId = st.stepid, stepNo = st.stepNo, description = st.stepDescription, image = st.stepImg }).ToList();

                return Request.CreateResponse(HttpStatusCode.OK, new { message = "Success", solution = solution, steps = steps });
            }
            catch (Exception ex) { return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message); }
        }

        // ======================================================
        // POST: api/solutions
        // ======================================================
        [HttpPost]
        [Route("")]
        public HttpResponseMessage AddSolution([FromBody] Solution model)
        {
            try
            {
                if (model == null || string.IsNullOrWhiteSpace(model.stitle)) return Request.CreateResponse(HttpStatusCode.BadRequest, "Title required.");
                var solution = new Solution { stitle = model.stitle.Trim(), date = DateTime.Now };
                db.Solutions.Add(solution);
                db.SaveChanges();
                return Request.CreateResponse(HttpStatusCode.Created, new { message = "Created.", solutionId = solution.sid });
            }
            catch (Exception ex) { return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message); }
        }

        // ======================================================
        // GET: api/solutions
        // ======================================================
        [HttpGet]
        [Route("")]
        public HttpResponseMessage GetAllSolutions()
        {
            try
            {
                var solutions = db.Solutions.Select(s => new { s.sid, title = s.stitle, s.date }).OrderByDescending(s => s.date).ToList();
                return Request.CreateResponse(HttpStatusCode.OK, new { total = solutions.Count, solutions = solutions });
            }
            catch (Exception ex) { return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message); }
        }

        // ======================================================
        // PUT: api/solutions/{sid} (Sirf Title Update)
        // ======================================================
        [HttpPut]
        [Route("{sid:int}")]
        public HttpResponseMessage UpdateSolutionTitle(int sid, [FromBody] Solution model)
        {
            try
            {
                if (sid <= 0) return Request.CreateResponse(HttpStatusCode.BadRequest, "Valid id required.");
                var solution = db.Solutions.FirstOrDefault(s => s.sid == sid);
                if (solution == null) return Request.CreateResponse(HttpStatusCode.NotFound, "Solution not found.");
                if (model == null || string.IsNullOrWhiteSpace(model.stitle)) return Request.CreateResponse(HttpStatusCode.BadRequest, "Title required.");

                solution.stitle = model.stitle.Trim();
                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, new { message = "Solution title updated." });
            }
            catch (Exception ex) { return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message); }
        }

        // ======================================================
        // DELETE: api/solutions/{sid}
        // ======================================================
        [HttpDelete]
        [Route("{sid:int}")]
        public HttpResponseMessage DeleteSolution(int sid)
        {
            try
            {
                var solution = db.Solutions.FirstOrDefault(s => s.sid == sid);
                if (solution == null) return Request.CreateResponse(HttpStatusCode.NotFound, "Solution not found.");

                var vpsLinks = db.VehicleProblemSolutions.Where(v => v.sid == sid).ToList();
                if (vpsLinks.Any()) return Request.CreateResponse(HttpStatusCode.BadRequest, "Cannot delete, it is linked.");

                db.Steps.RemoveRange(db.Steps.Where(s => s.sid == sid));
                db.UserRatings.RemoveRange(db.UserRatings.Where(r => r.sid == sid));
                db.ExpertSolutions.RemoveRange(db.ExpertSolutions.Where(e => e.sid == sid));
                db.Solutions.Remove(solution);
                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, new { message = "Deleted successfully." });
            }
            catch (Exception ex) { return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message); }
        }

        // ======================================================
        // 🔥 NEW API: PUT api/solutions/updatestep/{stepId}
        // Asaan Tareeqa + Khubsurat Naam!
        // ======================================================
        [HttpPut]
        [Route("updatestep/{stepId:int}")]
        public HttpResponseMessage UpdateStepImageAndDesc(int stepId)
        {
            try
            {
                var step = db.Steps.FirstOrDefault(s => s.stepid == stepId);
                if (step == null) return Request.CreateResponse(HttpStatusCode.NotFound, "Step not found.");

                var httpRequest = System.Web.HttpContext.Current.Request;

                // 1. Description update
                if (httpRequest.Form["description"] != null)
                {
                    step.stepDescription = httpRequest.Form["description"];
                }

                // 2. Nayi tasveer check aur save
                if (httpRequest.Files.Count > 0)
                {
                    var file = httpRequest.Files[0];
                    if (file != null && file.ContentLength > 0)
                    {
                        string oldImgPath = step.stepImg;
                        string cleanBaseName = "Solution_" + step.sid + "_Step_" + step.stepNo; // Fallback naam

                        // 👇 DATABASE SE PURANA PYARA NAAM NIKAL RAHE HAIN 👇
                        if (!string.IsNullOrEmpty(oldImgPath))
                        {
                            cleanBaseName = System.IO.Path.GetFileNameWithoutExtension(oldImgPath);

                            // Agar pehle se "_updated" laga hua hai tou hata dein, taake double na lag jaye
                            if (cleanBaseName.EndsWith("_updated"))
                            {
                                cleanBaseName = cleanBaseName.Replace("_updated", "");
                            }
                        }

                        string extension = System.IO.Path.GetExtension(file.FileName);
                        if (string.IsNullOrEmpty(extension)) extension = ".jpg";

                        // 👇 NAYA NAAM: Purana Naam + "_updated" 👇
                        string newName = cleanBaseName + "_updated" + extension;

                        string root = System.Web.HttpContext.Current.Server.MapPath("~/Images/Steps");
                        if (!System.IO.Directory.Exists(root)) System.IO.Directory.CreateDirectory(root);

                        string newPath = System.IO.Path.Combine(root, newName);
                        file.SaveAs(newPath);

                        // 👇 DB MEIN PATH BHI PYARA WALA SAVE HOGA 👇
                        step.stepImg = "/Images/Steps/" + newName;
                    }
                }

                db.SaveChanges();
                return Request.CreateResponse(HttpStatusCode.OK, new { message = "Step updated successfully." });
            }
            catch (Exception ex) { return Request.CreateResponse(HttpStatusCode.InternalServerError, "Error: " + ex.Message); }
        }
    }
}



//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net;
//using System.Net.Http;
//using System.Web.Http;

//namespace RyC.Controllers
//{
//    [RoutePrefix("api/solutions")]
//    public class SolutionsController : ApiController
//    {
//        private readonly RYCEntities db = new RYCEntities();

//        // ======================================================
//        // GET: api/solutions/{sid}
//        // Get full solution with steps
//        // ======================================================
//        [HttpGet]
//        [Route("{sid:int}")]
//        public HttpResponseMessage GetSolutionById(int sid)
//        {
//            try
//            {
//                if (sid <= 0)
//                {
//                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Valid solution id required.");
//                }

//                var solution = db.Solutions
//                    .Where(s => s.sid == sid)
//                    .Select(s => new
//                    {
//                        sid = s.sid,
//                        title = s.stitle,
//                        date = s.date
//                    })
//                    .FirstOrDefault();

//                if (solution == null)
//                {
//                    return Request.CreateResponse(HttpStatusCode.NotFound, "Solution not found.");
//                }

//                var steps = db.Steps
//                    .Where(st => st.sid == sid)
//                    .OrderBy(st => st.stepNo)
//                    .Select(st => new
//                    {
//                        stepId = st.stepid,
//                        stepNo = st.stepNo,
//                        description = st.stepDescription,
//                        image = st.stepImg
//                    })
//                    .ToList();

//                return Request.CreateResponse(HttpStatusCode.OK, new
//                {
//                    message = "Success",
//                    solution = solution,
//                    steps = steps
//                });
//            }
//            catch (Exception ex)
//            {
//                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
//            }
//        }

//        // ======================================================
//        // POST: api/solutions
//        // Add standalone solution
//        // ======================================================
//        [HttpPost]
//        [Route("")]
//        public HttpResponseMessage AddSolution([FromBody] Solution model)
//        {
//            try
//            {
//                if (model == null || string.IsNullOrWhiteSpace(model.stitle))
//                {
//                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Title required.");
//                }

//                var solution = new Solution
//                {
//                    stitle = model.stitle.Trim(),
//                    date = DateTime.Now
//                };

//                db.Solutions.Add(solution);
//                db.SaveChanges();

//                return Request.CreateResponse(HttpStatusCode.Created, new
//                {
//                    message = "Created.",
//                    solutionId = solution.sid
//                });
//            }
//            catch (Exception ex)
//            {
//                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
//            }
//        }

//        // ======================================================
//        // GET: api/solutions
//        // Get all solutions
//        // ======================================================
//        [HttpGet]
//        [Route("")]
//        public HttpResponseMessage GetAllSolutions()
//        {
//            try
//            {
//                var solutions = db.Solutions
//                    .Select(s => new
//                    {
//                        s.sid,
//                        title = s.stitle,
//                        s.date
//                    })
//                    .OrderByDescending(s => s.date)
//                    .ToList();

//                return Request.CreateResponse(HttpStatusCode.OK, new
//                {
//                    total = solutions.Count,
//                    solutions = solutions
//                });
//            }
//            catch (Exception ex)
//            {
//                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
//            }
//        }

//        // ======================================================
//        // PUT: api/solutions/{sid} 
//        // Update ONLY Solution Title
//        // ======================================================
//        [HttpPut]
//        [Route("{sid:int}")]
//        public HttpResponseMessage UpdateSolutionTitle(int sid, [FromBody] Solution model)
//        {
//            try
//            {
//                if (sid <= 0)
//                {
//                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Valid id required.");
//                }

//                var solution = db.Solutions.FirstOrDefault(s => s.sid == sid);

//                if (solution == null)
//                {
//                    return Request.CreateResponse(HttpStatusCode.NotFound, "Solution not found.");
//                }

//                if (model == null || string.IsNullOrWhiteSpace(model.stitle))
//                {
//                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Title required.");
//                }

//                solution.stitle = model.stitle.Trim();
//                db.SaveChanges();

//                return Request.CreateResponse(HttpStatusCode.OK, new
//                {
//                    message = "Solution title updated."
//                });
//            }
//            catch (Exception ex)
//            {
//                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
//            }
//        }

//        // ======================================================
//        // DELETE: api/solutions/{sid}
//        // ======================================================
//        [HttpDelete]
//        [Route("{sid:int}")]
//        public HttpResponseMessage DeleteSolution(int sid)
//        {
//            try
//            {
//                var solution = db.Solutions.FirstOrDefault(s => s.sid == sid);

//                if (solution == null)
//                {
//                    return Request.CreateResponse(HttpStatusCode.NotFound, "Solution not found.");
//                }

//                var vpsLinks = db.VehicleProblemSolutions.Where(v => v.sid == sid).ToList();
//                if (vpsLinks.Any())
//                {
//                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Cannot delete, it is linked.");
//                }

//                db.Steps.RemoveRange(db.Steps.Where(s => s.sid == sid));
//                db.UserRatings.RemoveRange(db.UserRatings.Where(r => r.sid == sid));
//                db.ExpertSolutions.RemoveRange(db.ExpertSolutions.Where(e => e.sid == sid));
//                db.Solutions.Remove(solution);
//                db.SaveChanges();

//                return Request.CreateResponse(HttpStatusCode.OK, new
//                {
//                    message = "Deleted successfully."
//                });
//            }
//            catch (Exception ex)
//            {
//                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
//            }
//        }

//        // ======================================================
//        // 🔥 NEW API: PUT api/solutions/updatestep/{stepId}
//        // Asaan Tareeqa + Khubsurat Naming (Without GUID)
//        // ======================================================
//        [HttpPut]
//        [Route("updatestep/{stepId:int}")]
//        public HttpResponseMessage UpdateStepImageAndDesc(int stepId)
//        {
//            try
//            {
//                var step = db.Steps.FirstOrDefault(s => s.stepid == stepId);

//                if (step == null)
//                {
//                    return Request.CreateResponse(HttpStatusCode.NotFound, "Step not found.");
//                }

//                var httpRequest = System.Web.HttpContext.Current.Request;

//                // 1. Description update karein
//                if (httpRequest.Form["description"] != null)
//                {
//                    step.stepDescription = httpRequest.Form["description"];
//                }

//                // 2. Nayi tasveer check aur save (Khubsurat Naam ke sath)
//                if (httpRequest.Files.Count > 0)
//                {
//                    var file = httpRequest.Files[0];
//                    if (file != null && file.ContentLength > 0)
//                    {
//                        // Purana pyara sa naam database se nikalne ka logic
//                        string oldImgPath = step.stepImg;
//                        string cleanBaseName = $"Solution_{step.sid}_Step_{step.stepNo}"; // Default naam agar purana na milay

//                        if (!string.IsNullOrEmpty(oldImgPath))
//                        {
//                            // Sirf naam nikalo (Extension aur folder path hata do)
//                            cleanBaseName = System.IO.Path.GetFileNameWithoutExtension(oldImgPath);

//                            // Agar pehle se "_updated" likha hai tou usay hata dein, taake repeat na ho
//                            if (cleanBaseName.EndsWith("_updated"))
//                            {
//                                cleanBaseName = cleanBaseName.Replace("_updated", "");
//                            }
//                        }

//                        // File ki extension (.jpg, .png)
//                        string extension = System.IO.Path.GetExtension(file.FileName);
//                        if (string.IsNullOrEmpty(extension)) extension = ".jpg";

//                        // Naya khubsurat naam (e.g., Alto_S1_Step2_ID8_updated.jpg)
//                        string newName = cleanBaseName + "_updated" + extension;

//                        // Folder wahi purana set kar diya: /Images/Steps/
//                        string root = System.Web.HttpContext.Current.Server.MapPath("~/Images/Steps");

//                        if (!System.IO.Directory.Exists(root))
//                        {
//                            System.IO.Directory.CreateDirectory(root);
//                        }

//                        // Tasveer Save ki
//                        string newPath = System.IO.Path.Combine(root, newName);
//                        file.SaveAs(newPath);

//                        // Database mein naya pyara path save
//                        step.stepImg = "/Images/Steps/" + newName;
//                    }
//                }

//                db.SaveChanges();

//                return Request.CreateResponse(HttpStatusCode.OK, new
//                {
//                    message = "Step updated successfully."
//                });
//            }
//            catch (Exception ex)
//            {
//                return Request.CreateResponse(HttpStatusCode.InternalServerError, "Error: " + ex.Message);
//            }
//        }
//    }
//}
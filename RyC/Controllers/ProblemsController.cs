using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using RyC;

namespace RyC.Controllers
{
    [RoutePrefix("api/problems")]
    public class ProblemsController : ApiController
    {
        private readonly RYCEntities db = new RYCEntities();

        // POST: api/problems
        [HttpPost]
        [Route("addprob")]
        public HttpResponseMessage AddProblem([FromBody] Problem model)
        {
            try
            {
                if (model == null)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Problem data required."
                    );
                }

                if (string.IsNullOrWhiteSpace(model.ptitle) ||
                    string.IsNullOrWhiteSpace(model.ptype))
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Problem title, type Required."
                    );
                }

                var problemType = model.ptype.ToLower();
                var allowedTypes = new[] { "electrical", "mechanical" };

                if (!allowedTypes.Contains(problemType))
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "ptype just 'electrical' or 'mechanical'."
                    );
                }

                var addedBy = string.IsNullOrWhiteSpace(model.type)
                    ? "expert"
                    : model.type.ToLower();

                var exists = db.Problems.Any(p =>
                    p.ptitle == model.ptitle &&
                    p.ptype == problemType
                );

                if (exists)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.Conflict,   // 409
                        "problem title exist with this type."
                    );
                }

                var problem = new Problem
                {
                    ptitle = model.ptitle,
                    pdescription = model.pdescription,
                    ptype = problemType,
                    type = addedBy,
                    uid = model.uid
                };

                db.Problems.Add(problem);
                db.SaveChanges();

                var responseBody = new
                {
                    message = "Problem added successfully.",
                    pid = problem.pid,
                    ptitle = problem.ptitle,
                    pdescription = problem.pdescription,
                    ptype = problem.ptype,
                    type = problem.type,
                    uid = problem.uid
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

        // GET: api/problems/title?ptitle=xyz
        [HttpGet]
        [Route("title")]
        public HttpResponseMessage GetProblemByTitle(string ptitle)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ptitle))
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Problem title (ptitle) is required."
                    );
                }

                var problem = db.Problems
                    .Where(p => p.ptitle.ToLower() == ptitle.ToLower())
                    .Select(p => new
                    {
                        pid = p.pid,
                        ptitle = p.ptitle,
                        pdescription = p.pdescription,
                        ptype = p.ptype,
                        type = p.type,
                        uid = p.uid
                    })
                    .FirstOrDefault();

                if (problem == null)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "No problem found with this title."
                    );
                }

                return Request.CreateResponse(
                    HttpStatusCode.OK,
                    problem
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

        // GET: api/problems/3
        [HttpGet]
        [Route("{pid:int}")]
        public HttpResponseMessage GetProblemById(int pid)
        {
            try
            {
                if (pid <= 0)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Valid problem id (pid) required."
                    );
                }

                var problem = db.Problems
                    .Where(p => p.pid == pid)
                    .Select(p => new
                    {
                        pid = p.pid,
                        ptitle = p.ptitle,
                        pdescription = p.pdescription,
                        ptype = p.ptype,   // electrical / mechanical
                        type = p.type,     // admin / expert / user
                        uid = p.uid
                    })
                    .FirstOrDefault();

                if (problem == null)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "Problem not found."
                    );
                }

                return Request.CreateResponse(
                    HttpStatusCode.OK,
                    problem
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

        // GET: api/problems
        [HttpGet]
        [Route("getallproblem")]
        public HttpResponseMessage GetAllProblems()
        {
            try
            {
                var problems = db.Problems
                    .Select(p => new
                    {
                        pid = p.pid,
                        ptitle = p.ptitle,
                        pdescription = p.pdescription,
                        ptype = p.ptype,
                        type = p.type,
                        uid = p.uid
                    })
                    .OrderBy(p => p.ptitle)
                    .ToList();

                var response = new
                {
                    message = "All problems fetched successfully.",
                    total = problems.Count,
                    problems = problems
                };

                return Request.CreateResponse(
                    HttpStatusCode.OK,
                    response
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
        // PUT: api/problems/{pid}
        // Update problem details
        // ======================================================
        [HttpPut]
        [Route("{pid:int}")]
        public HttpResponseMessage UpdateProblem(int pid, [FromBody] Problem model)
        {
            try
            {
                if (pid <= 0)
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        "Valid problem id required.");

                var problem = db.Problems.FirstOrDefault(p => p.pid == pid);
                if (problem == null)
                    return Request.CreateResponse(HttpStatusCode.NotFound,
                        "Problem not found.");

                if (model == null)
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        "Problem data required.");

                if (string.IsNullOrWhiteSpace(model.ptitle) ||
                    string.IsNullOrWhiteSpace(model.ptype))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        "Problem title and type required.");
                }

                var problemType = model.ptype.ToLower();
                var allowedTypes = new[] { "electrical", "mechanical" };

                if (!allowedTypes.Contains(problemType))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        "ptype must be 'electrical' or 'mechanical'.");
                }

                // Duplicate check (exclude current problem)
                bool exists = db.Problems.Any(p =>
                    p.ptitle == model.ptitle &&
                    p.ptype == problemType &&
                    p.pid != pid);

                if (exists)
                {
                    return Request.CreateResponse(HttpStatusCode.Conflict,
                        "Problem title already exists with this type.");
                }

                problem.ptitle = model.ptitle.Trim();
                problem.pdescription = model.pdescription;
                problem.ptype = problemType;
                problem.type = model.type;

                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    message = "Problem updated successfully.",
                    pid = problem.pid,
                    ptitle = problem.ptitle,
                    pdescription = problem.pdescription,
                    ptype = problem.ptype
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
        // DELETE: api/problems/{pid}
        // Safe delete problem
        // ======================================================
        [HttpDelete]
        [Route("{pid:int}")]
        public HttpResponseMessage DeleteProblem(int pid)
        {
            try
            {
                if (pid <= 0)
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        "Valid problem id required.");

                var problem = db.Problems.FirstOrDefault(p => p.pid == pid);
                if (problem == null)
                    return Request.CreateResponse(HttpStatusCode.NotFound,
                        "Problem not found.");

                // Check if linked to any vehicle solutions
                bool isLinked = db.VehicleProblemSolutions.Any(v => v.pid == pid);

                if (isLinked)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        "Cannot delete problem. It is linked with existing solutions.");
                }

                db.Problems.Remove(problem);
                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    message = "Problem deleted successfully.",
                    deletedProblemId = pid
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(
                    HttpStatusCode.InternalServerError,
                    "Something went wrong: " + ex.Message);
            }
        }
    }
}
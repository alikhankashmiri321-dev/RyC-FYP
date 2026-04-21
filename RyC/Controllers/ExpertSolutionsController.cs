using RyC;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace RYC.Controllers
{
    [RoutePrefix("api/expertsolutions")]
    public class ExpertSolutionsController : ApiController
    {
        private readonly RYCEntities db = new RYCEntities();

        // ======================================================
        // 1️⃣ POST: Expert ↔ Solution link
        // URL: POST api/expertsolutions?eid=3&sid=12
        // ======================================================
        [HttpPost]
        [Route("")]
        public HttpResponseMessage LinkExpertSolution(int eid, int sid)
        {
            try
            {
                // Basic validation
                if (eid <= 0 || sid <= 0)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Valid eid and sid required."
                    );
                }

                // Expert exists?
                bool expertExists = db.Experts.Any(e => e.eid == eid);
                if (!expertExists)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "Expert not found."
                    );
                }

                // Solution exists?
                bool solutionExists = db.Solutions.Any(s => s.sid == sid);
                if (!solutionExists)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "Solution not found."
                    );
                }

                // Duplicate check
                bool alreadyLinked = db.ExpertSolutions
                    .Any(x => x.eid == eid && x.sid == sid);

                if (alreadyLinked)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.Conflict,
                        "This expert is already linked with this solution."
                    );
                }

                // Insert
                ExpertSolution link = new ExpertSolution
                {
                    eid = eid,
                    sid = sid
                };

                db.ExpertSolutions.Add(link);
                db.SaveChanges();

                return Request.CreateResponse(
                    HttpStatusCode.Created,
                    new
                    {
                        message = "ExpertSolution linked successfully.",
                        esid = link.esid,
                        eid = link.eid,
                        sid = link.sid
                    }
                );
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(
                    HttpStatusCode.InternalServerError,
                    ex.Message
                );
            }
        }

        // ======================================================
        // 2️⃣ GET: Expert ke solutions list
        // URL: GET api/expertsolutions/expert/3
        // ======================================================
        [HttpGet]
        [Route("expert/{eid:int}")]
        public HttpResponseMessage GetSolutionsByExpert(int eid)
        {
            try
            {
                if (eid <= 0)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Valid eid required."
                    );
                }

                bool expertExists = db.Experts.Any(e => e.eid == eid);
                if (!expertExists)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "Expert not found."
                    );
                }

                var data = db.ExpertSolutions
                    .Where(es => es.eid == eid)
                    .Select(es => new
                    {
                        esid = es.esid,
                        sid = es.sid,

                        // Optional: Solution basic info
                        solution = db.Solutions
                            .Where(s => s.sid == es.sid)
                            .Select(s => new
                            {
                                sid = s.sid,
                                title = s.stitle
                                // yahan title/description add kar sakte ho
                            })
                            .FirstOrDefault()
                    })
                    .ToList();

                return Request.CreateResponse(
                    HttpStatusCode.OK,
                    data
                );
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(
                    HttpStatusCode.InternalServerError,
                    ex.Message
                );
            }
        }

        // ======================================================
        // GET: api/expertsolutions
        // Get all expert-solution links
        // ======================================================
        [HttpGet]
        [Route("")]
        public HttpResponseMessage GetAllExpertSolutions()
        {
            try
            {
                var data = db.ExpertSolutions
                    .Select(es => new
                    {
                        es.esid,
                        es.eid,
                        ExpertName = es.Expert.User.username, // Important fix
                        es.sid,
                        SolutionTitle = es.Solution.stitle
                    })
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    message = "All expert-solution assignments fetched successfully.",
                    total = data.Count,
                    assignments = data
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(
                    HttpStatusCode.InternalServerError,
                    ex.Message
                );
            }
        }
        // ======================================================
        // GET: api/expertsolutions/solution/{sid}
        // Get experts assigned to a solution
        // ======================================================
        [HttpGet]
        [Route("solution/{sid:int}")]
        public HttpResponseMessage GetExpertsBySolution(int sid)
        {
            try
            {
                if (sid <= 0)
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Valid sid required."
                    );

                bool solutionExists = db.Solutions.Any(s => s.sid == sid);
                if (!solutionExists)
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "Solution not found."
                    );

                var data = db.ExpertSolutions
                    .Where(es => es.sid == sid)
                    .Select(es => new
                    {
                        es.esid,
                        es.eid,
                        ExpertName = es.Expert.User.username,  // Correct navigation
                        ExpertCategory = es.Expert.category
                    })
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    message = "Experts fetched successfully.",
                    total = data.Count,
                    experts = data
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(
                    HttpStatusCode.InternalServerError,
                    ex.Message
                );
            }
        }
        // ======================================================
        // DELETE: api/expertsolutions/{esid}
        // Delete ExpertSolution record
        // ======================================================
        [HttpDelete]
        [Route("{esid:int}")]
        public HttpResponseMessage DeleteExpertSolution(int esid)
        {
            try
            {
                if (esid <= 0)
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Valid ExpertSolution id required."
                    );

                var record = db.ExpertSolutions
                               .FirstOrDefault(x => x.esid == esid);

                if (record == null)
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "ExpertSolution record not found."
                    );

                db.ExpertSolutions.Remove(record);
                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    message = "ExpertSolution deleted successfully.",
                    deletedEsid = esid
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(
                    HttpStatusCode.InternalServerError,
                    ex.Message
                );
            }
        }
    }
}

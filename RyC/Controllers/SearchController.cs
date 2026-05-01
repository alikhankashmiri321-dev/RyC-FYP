using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace RyC.Controllers
{
    [RoutePrefix("api/search")]
    public class SearchController : ApiController
    {
        private readonly RYCEntities db = new RYCEntities();

        //  GET: api/search/full?query=tyre flat&make=Honda&model=Civic&variant=EX&year=2000
        [HttpGet]
        [Route("full")]
        public HttpResponseMessage FullSearch(
            [FromUri] string query,
            [FromUri] string make,
            [FromUri] string model,
            [FromUri] string variant,
            [FromUri] short year)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Message = "query is required" });

                if (string.IsNullOrWhiteSpace(make) || string.IsNullOrWhiteSpace(model) ||
                    string.IsNullOrWhiteSpace(variant))
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Message = "make, model, variant are required" });

                query = query.Trim();
                make = make.Trim();
                model = model.Trim();
                variant = variant.Trim();

                var vehicle = db.Vehicles
                    .Where(v => v.make == make && v.model == model && v.variant == variant && v.year == year)
                    .Select(v => new { v.vid, v.make, v.model, v.variant, v.year })
                    .FirstOrDefault();

                if (vehicle == null)
                    return Request.CreateResponse(HttpStatusCode.NotFound, new { Message = "Vehicle not found" });

                var problems = db.Problems
                    .Where(p => p.ptitle.Contains(query))
                    .Select(p => new { p.pid, title = p.ptitle, addedByUserId = p.uid, p.type })
                    .ToList();

                if (problems.Count == 0)
                    return Request.CreateResponse(HttpStatusCode.NotFound, new { Message = "No matching problems found" });

                var pids = problems.Select(x => x.pid).ToList();

                var vpsRows = db.VehicleProblemSolutions
                    .Where(x => x.vid == vehicle.vid && pids.Contains(x.pid))
                    .ToList();

                if (vpsRows.Count == 0)
                    return Request.CreateResponse(HttpStatusCode.NotFound, new { Message = "No solutions found for this vehicle and searched problem" });

                var response = new
                {
                    vehicle = vehicle,
                    query = query,

                    problems = problems.Select(p => new
                    {
                        pid = p.pid,
                        title = p.title,
                        type = p.type,
                        addedByUserId = p.addedByUserId,

                        addedBy = (from u in db.Users
                                   where u.uid == p.addedByUserId
                                   select new { u.uid, u.username, u.type }).FirstOrDefault(),

                        // 👇 SOLUTIONS SORTING LOGIC (Bilkul Saada Tareeqa) 👇
                        solutions = vpsRows
                            .Where(x => x.pid == p.pid)
                            .Select(x => new
                            {
                                sid = x.sid,
                                solutionTitle = db.Solutions.Where(s => s.sid == x.sid).Select(s => s.stitle).FirstOrDefault(),

                                // 🔥 Kyunke ye non-nullable decimal hai, toh direct uthayenge
                                overallRating = x.overallrating,

                                solutionExpert = (from e in db.Experts
                                                  join u in db.Users on e.uid equals u.uid
                                                  where e.eid == x.eid
                                                  select new { e.eid, expertName = u.username, e.category }).FirstOrDefault(),

                                steps = db.Steps
                                    .Where(st => st.sid == x.sid)
                                    .OrderBy(st => st.stepNo)
                                    .Select(st => new { st.stepNo, st.stepImg, st.stepDescription })
                                    .ToList()
                            })
                            .OrderByDescending(x => x.overallRating) // Sorting descending order mein
                            .ToList()
                    }).ToList()
                };

                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new { Message = "Server error", Error = ex.Message });
            }
        }

        //  GET:  api/search/full/default?query=tyre flat&uid=1009
        [HttpGet]
        [Route("full/default")]
        public HttpResponseMessage FullSearchDefault([FromUri] string query, [FromUri] int uid)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Message = "query is required" });

                if (uid <= 0)
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Message = "uid is required" });

                query = query.Trim();

                var user = db.Users.FirstOrDefault(u => u.uid == uid);
                if (user == null) return Request.CreateResponse(HttpStatusCode.NotFound, new { Message = "User not found" });
                if (user.default_vid == null) return Request.CreateResponse(HttpStatusCode.BadRequest, new { Message = "Default vehicle not set" });

                int vehicleId = user.default_vid.Value;
                var vehicle = db.Vehicles.Where(v => v.vid == vehicleId).Select(v => new { v.vid, v.make, v.model, v.variant, v.year }).FirstOrDefault();

                if (vehicle == null) return Request.CreateResponse(HttpStatusCode.NotFound, new { Message = "Default vehicle not found" });

                var problems = db.Problems.Where(p => p.ptitle.Contains(query)).Select(p => new { p.pid, title = p.ptitle, addedByUserId = p.uid, p.type }).ToList();

                if (problems.Count == 0) return Request.CreateResponse(HttpStatusCode.NotFound, new { Message = "No matching problems found" });

                var pids = problems.Select(x => x.pid).ToList();
                var vpsRows = db.VehicleProblemSolutions.Where(x => x.vid == vehicle.vid && pids.Contains(x.pid)).ToList();

                var response = new
                {
                    user = new { user.uid, user.username, user.type, user.default_vid },
                    vehicle = vehicle,
                    query = query,

                    problems = problems.Select(p => new
                    {
                        pid = p.pid,
                        title = p.title,
                        type = p.type,
                        addedByUserId = p.addedByUserId,

                        addedBy = (from u in db.Users where u.uid == p.addedByUserId select new { u.uid, u.username, u.type }).FirstOrDefault(),

                        solutions = vpsRows
                            .Where(x => x.pid == p.pid)
                            .Select(x => new
                            {
                                sid = x.sid,
                                solutionTitle = db.Solutions.Where(s => s.sid == x.sid).Select(s => s.stitle).FirstOrDefault(),

                                // 🔥 Direct value kyunke decimal null nahi ho sakta
                                overallRating = x.overallrating,

                                solutionExpert = (from e in db.Experts
                                                  join u in db.Users on e.uid equals u.uid
                                                  where e.eid == x.eid
                                                  select new { e.eid, expertName = u.username, e.category }).FirstOrDefault(),

                                steps = db.Steps.Where(st => st.sid == x.sid).OrderBy(st => st.stepNo)
                                    .Select(st => new { st.stepNo, st.stepImg, st.stepDescription }).ToList()
                            })
                            .OrderByDescending(x => x.overallRating) // Sorting
                            .ToList()
                    }).ToList()
                };

                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new { Message = "Server error", Error = ex.Message });
            }
        }
    }
}
//using System;
//using System.Linq;
//using System.Net;
//using System.Net.Http;
//using System.Web.Http;

//namespace RyC.Controllers
//{
//    [RoutePrefix("api/search")]
//    public class SearchController : ApiController
//    {
//        private readonly RYCEntities db = new RYCEntities();

//        //  GET: api/search/full?query=tyre flat&make=Honda&model=Civic&variant=EX&year=2000
//        [HttpGet]
//        [Route("full")]
//        public HttpResponseMessage FullSearch(
//            [FromUri] string query,
//            [FromUri] string make,
//            [FromUri] string model,
//            [FromUri] string variant,
//            [FromUri] short year)
//        {
//            try
//            {

//                if (string.IsNullOrWhiteSpace(query))
//                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Message = "query is required" });

//                if (string.IsNullOrWhiteSpace(make) || string.IsNullOrWhiteSpace(model) ||
//                    string.IsNullOrWhiteSpace(variant))
//                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Message = "make, model, variant are required" });

//                query = query.Trim();
//                make = make.Trim();
//                model = model.Trim();
//                variant = variant.Trim();


//                var vehicle = db.Vehicles
//                    .Where(v => v.make == make && v.model == model && v.variant == variant && v.year == year)
//                    .Select(v => new
//                    {
//                        v.vid,
//                        v.make,
//                        v.model,
//                        v.variant,
//                        v.year
//                    })
//                    .FirstOrDefault();

//                if (vehicle == null)
//                    return Request.CreateResponse(HttpStatusCode.NotFound, new { Message = "Vehicle not found" });


//                var problems = db.Problems
//                    .Where(p => p.ptitle.Contains(query))
//                    .Select(p => new
//                    {
//                        p.pid,
//                        title = p.ptitle,
//                        addedByUserId = p.uid,     
//                        p.type                      
//                    })
//                    .ToList();

//                if (problems.Count == 0)
//                    return Request.CreateResponse(HttpStatusCode.NotFound, new { Message = "No matching problems found" });

//                var pids = problems.Select(x => x.pid).ToList();

//                var vpsRows = db.VehicleProblemSolutions
//                    .Where(x => x.vid == vehicle.vid && pids.Contains(x.pid))
//                    .ToList();

//                if (vpsRows.Count == 0)
//                    return Request.CreateResponse(HttpStatusCode.NotFound, new { Message = "No solutions found for this vehicle and searched problem" });

//                var response = new
//                {
//                    vehicle = vehicle,
//                    query = query,

//                    problems = problems.Select(p => new
//                    {
//                        pid = p.pid,
//                        title = p.title,
//                        type = p.type,
//                        addedByUserId = p.addedByUserId,

//                        addedBy = (from u in db.Users
//                                   where u.uid == p.addedByUserId
//                                   select new
//                                   {
//                                       u.uid,
//                                       u.username,
//                                       u.type 
//                                   }).FirstOrDefault(),


//                        solutions = vpsRows
//                            .Where(x => x.pid == p.pid)
//                            .OrderByDescending(x => x.overallrating)
//                            .Select(x => new
//                            {
//                                sid = x.sid,
//                                solutionTitle = db.Solutions
//                                    .Where(s => s.sid == x.sid)
//                                    .Select(s => s.stitle)
//                                    .FirstOrDefault(),

//                                overallRating = x.overallrating,

//                                solutionExpert = (from e in db.Experts
//                                                  join u in db.Users on e.uid equals u.uid
//                                                  where e.eid == x.eid
//                                                  select new
//                                                  {
//                                                      e.eid,
//                                                      expertName = u.username,
//                                                      e.category
//                                                  }).FirstOrDefault(),

//                                steps = db.Steps
//                                    .Where(st => st.sid == x.sid)
//                                    .OrderBy(st => st.stepNo)
//                                    .Select(st => new
//                                    {
//                                        st.stepNo,
//                                        st.stepImg,
//                                        st.stepDescription
//                                    })
//                                    .ToList()
//                            })
//                            .ToList()
//                    })
//                    .ToList()
//                };

//                return Request.CreateResponse(HttpStatusCode.OK, response);
//            }
//            catch (Exception ex)
//            {
//                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
//                {
//                    Message = "Server error",
//                    Error = ex.Message
//                });
//            }
//        }

//        //  GET:  api/search/full/default?query=tyre flat&uid=1009
//        [HttpGet]
//        [Route("full/default")]
//        public HttpResponseMessage FullSearchDefault(
//            [FromUri] string query,
//            [FromUri] int uid)
//        {
//            try
//            {
//                if (string.IsNullOrWhiteSpace(query))
//                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Message = "query is required" });

//                if (uid <= 0)
//                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Message = "uid is required" });

//                query = query.Trim();

//                var user = db.Users.FirstOrDefault(u => u.uid == uid);
//                if (user == null)
//                    return Request.CreateResponse(HttpStatusCode.NotFound, new { Message = "User not found" });

//                if (user.default_vid == null)
//                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Message = "Default vehicle not set for this user" });

//                int vehicleId = user.default_vid.Value;

//                var vehicle = db.Vehicles
//                    .Where(v => v.vid == vehicleId)
//                    .Select(v => new
//                    {
//                        v.vid,
//                        v.make,
//                        v.model,
//                        v.variant,
//                        v.year
//                    })
//                    .FirstOrDefault();

//                if (vehicle == null)
//                    return Request.CreateResponse(HttpStatusCode.NotFound, new { Message = "Default vehicle not found" });

//                var problems = db.Problems
//                    .Where(p => p.ptitle.Contains(query))
//                    .Select(p => new
//                    {
//                        p.pid,
//                        title = p.ptitle,
//                        addedByUserId = p.uid,
//                        p.type
//                    })
//                    .ToList();

//                if (problems.Count == 0)
//                    return Request.CreateResponse(HttpStatusCode.NotFound, new { Message = "No matching problems found" });

//                var pids = problems.Select(x => x.pid).ToList();

//                var vpsRows = db.VehicleProblemSolutions
//                    .Where(x => x.vid == vehicle.vid && pids.Contains(x.pid))
//                    .ToList();

//                if (vpsRows.Count == 0)
//                    return Request.CreateResponse(HttpStatusCode.NotFound, new { Message = "No solutions found for this default vehicle and searched problem" });

//                var response = new
//                {
//                    user = new { user.uid, user.username, user.type, user.default_vid },
//                    vehicle = vehicle,
//                    query = query,

//                    problems = problems.Select(p => new
//                    {
//                        pid = p.pid,
//                        title = p.title,
//                        type = p.type,
//                        addedByUserId = p.addedByUserId,

//                        addedBy = (from u in db.Users
//                                   where u.uid == p.addedByUserId
//                                   select new
//                                   {
//                                       u.uid,
//                                       u.username,
//                                       u.type
//                                   }).FirstOrDefault(),

//                        solutions = vpsRows
//                            .Where(x => x.pid == p.pid)
//                            .OrderByDescending(x => x.overallrating)
//                            .Select(x => new
//                            {
//                                sid = x.sid,
//                                solutionTitle = db.Solutions
//                                    .Where(s => s.sid == x.sid)
//                                    .Select(s => s.stitle)
//                                    .FirstOrDefault(),

//                                overallRating = x.overallrating,

//                                solutionExpert = (from e in db.Experts
//                                                  join u in db.Users on e.uid equals u.uid
//                                                  where e.eid == x.eid
//                                                  select new
//                                                  {
//                                                      e.eid,
//                                                      expertName = u.username,
//                                                      e.category
//                                                  }).FirstOrDefault(),

//                                steps = db.Steps
//                                    .Where(st => st.sid == x.sid)
//                                    .OrderBy(st => st.stepNo)
//                                    .Select(st => new
//                                    {
//                                        st.stepNo,
//                                        st.stepImg,
//                                        st.stepDescription
//                                    })
//                                    .ToList()
//                            })
//                            .ToList()
//                    })
//                    .ToList()
//                };

//                return Request.CreateResponse(HttpStatusCode.OK, response);
//            }
//            catch (Exception ex)
//            {
//                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
//                {
//                    Message = "Server error",
//                    Error = ex.Message
//                });
//            }
//        }

//    }
//}

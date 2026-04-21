using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using RyC;

namespace RyC.Controllers
{
    [RoutePrefix("api/vps")]
    public class VPSController : ApiController
    {
        private readonly RYCEntities db = new RYCEntities();

        // =====================================================
        // GET: api/vps
        // Get all VehicleProblemSolution records
        // =====================================================
        [HttpGet]
        [Route("")]
        public HttpResponseMessage GetAllVPS()
        {
            try
            {
                var data = db.VehicleProblemSolutions
                    .Select(v => new
                    {
                        v.id,
                        v.vid,
                        vehicle = v.Vehicle.make + " " + v.Vehicle.model,
                        v.pid,
                        problemTitle = v.Problem.ptitle,
                        v.sid,
                        solutionTitle = v.Solution.stitle,
                        v.eid,
                        expertId = v.eid,
                        overallRating = v.overallrating
                    })
                    .OrderByDescending(v => v.id)
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    message = "All VPS records fetched successfully.",
                    total = data.Count,
                    records = data
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


        // =====================================================
        // DELETE: api/vps/{id}
        // Delete VPS record safely
        // =====================================================
        [HttpDelete]
        [Route("{id:int}")]
        public HttpResponseMessage DeleteVPS(int id)
        {
            try
            {
                if (id <= 0)
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Valid VPS id required."
                    );

                var vps = db.VehicleProblemSolutions
                            .FirstOrDefault(x => x.id == id);

                if (vps == null)
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "VPS record not found."
                    );

                // Optional safety:
                // Check if ratings exist for this solution
                bool hasRatings = db.UserRatings
                                    .Any(r => r.sid == vps.sid);

                if (hasRatings)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Cannot delete VPS. Ratings exist for this solution."
                    );
                }

                db.VehicleProblemSolutions.Remove(vps);
                db.SaveChanges();

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    message = "VPS record deleted successfully.",
                    deletedId = id
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

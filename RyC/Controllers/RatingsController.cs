using RyC;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace RYC.Controllers
{
    [RoutePrefix("api/ratings")]
    public class RatingsController : ApiController
    {
        private readonly RYCEntities db = new RYCEntities();

        // =====================================================
        // POST: api/ratings
        // Add rating (prevent duplicate) + update VPS overallrating
        // =====================================================
        [HttpPost]
        [Route("")]
        public HttpResponseMessage AddRating(UserRating model)
        {
            try
            {
                // 1️⃣ Basic validation
                if (model == null || model.sid <= 0 || model.uid <= 0)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Valid sid and uid required."
                    );
                }

                // 👇 0 Rating (Skip) ko allow kar diya hai 👇
                if (model.rating < 0 || model.rating > 5)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Rating must be between 0 and 5."
                    );
                }

                // 2️⃣ Check solution exists
                bool solutionExists = db.Solutions.Any(s => s.sid == model.sid);
                if (!solutionExists)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "Solution not found."
                    );
                }

                // 3️⃣ Duplicate check (VERY IMPORTANT)
                bool alreadyRated = db.UserRatings
                    .Any(r => r.sid == model.sid && r.uid == model.uid);

                if (alreadyRated)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.Conflict,
                        "You have already rated this solution. Please update your rating."
                    );
                }

                // 4️⃣ Insert rating
                var newRating = new UserRating
                {
                    sid = model.sid,
                    uid = model.uid,
                    rating = model.rating,
                    review = model.review
                };

                db.UserRatings.Add(newRating);
                db.SaveChanges();

                // 5️⃣ Recalculate average rating (👇 Sirf > 0 wali count hongi 👇)
                decimal avgRating = db.UserRatings
                    .Where(r => r.sid == model.sid && r.rating > 0)
                    .Average(r => (decimal?)r.rating) ?? 0;

                avgRating = Math.Round(avgRating, 1);

                // 6️⃣ Update VPS overallrating
                var relatedVps = db.VehicleProblemSolutions
                    .Where(v => v.sid == model.sid)
                    .ToList();

                foreach (var vps in relatedVps)
                {
                    vps.overallrating = avgRating;
                }

                db.SaveChanges();

                return Request.CreateResponse(
                    HttpStatusCode.Created,
                    new
                    {
                        message = "Rating added successfully.",
                        ratingId = newRating.ratingid,
                        newAverage = avgRating
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

        // =====================================================
        // GET: api/ratings/solution/{sid}
        // Get all reviews for a solution
        // =====================================================
        [HttpGet]
        [Route("solution/{sid:int}")]
        public HttpResponseMessage GetRatingsBySolution(int sid)
        {
            try
            {
                if (sid <= 0)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Valid solution id required."
                    );
                }

                // Check solution exists
                bool solutionExists = db.Solutions.Any(s => s.sid == sid);
                if (!solutionExists)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "Solution not found."
                    );
                }

                // 👇 UI ke model (ReviewModel) ke mutabiq keys set ki hain 👇
                var ratings = db.UserRatings
                    .Where(r => r.sid == sid && r.rating > 0)
                    .Select(r => new
                    {
                        reviewerName = r.User.username != null ? r.User.username : "Unknown User",
                        rating = r.rating,
                        reviewText = r.review != null ? r.review : ""
                    })
                    .ToList();

                return Request.CreateResponse(
                    HttpStatusCode.OK,
                    ratings
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

        // =====================================================
        // GET: api/ratings/solution/{sid}/average
        // Get average rating for a solution
        // =====================================================
        [HttpGet]
        [Route("solution/{sid:int}/average")]
        public HttpResponseMessage GetAverageRating(int sid)
        {
            try
            {
                if (sid <= 0)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Valid solution id required."
                    );
                }

                // Check solution exists
                bool solutionExists = db.Solutions.Any(s => s.sid == sid);
                if (!solutionExists)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "Solution not found."
                    );
                }

                // 👇 Calculate average rating (sirf > 0 wali) 👇
                decimal avgRating = db.UserRatings
                    .Where(r => r.sid == sid && r.rating > 0)
                    .Average(r => (decimal?)r.rating) ?? 0;

                avgRating = Math.Round(avgRating, 1);

                return Request.CreateResponse(
                    HttpStatusCode.OK,
                    new
                    {
                        solutionId = sid,
                        averageRating = avgRating
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

        // =====================================================
        // GET: api/ratings/user/{uid}
        // Get all ratings given by a user
        // =====================================================
        [HttpGet]
        [Route("user/{uid:int}")]
        public HttpResponseMessage GetRatingsByUser(int uid)
        {
            try
            {
                if (uid <= 0)
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Valid user id required."
                    );

                var ratings = db.UserRatings
                    .Where(r => r.uid == uid)
                    .Select(r => new
                    {
                        r.ratingid,
                        r.sid,
                        SolutionTitle = r.Solution.stitle,
                        r.rating,
                        r.review
                    })
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    message = "User ratings fetched successfully.",
                    total = ratings.Count,
                    data = ratings
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

        // =====================================================
        // PUT: api/ratings/{ratingid}
        // Update rating + recalculate VPS overallrating
        // =====================================================
        [HttpPut]
        [Route("{ratingid:int}")]
        public HttpResponseMessage UpdateRating(int ratingid, [FromBody] UserRating model)
        {
            try
            {
                // 1️⃣ Validate rating id
                if (ratingid <= 0)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Valid rating id required."
                    );
                }

                // 2️⃣ Find existing rating
                var existingRating = db.UserRatings
                                       .FirstOrDefault(r => r.ratingid == ratingid);

                if (existingRating == null)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "Rating not found."
                    );
                }

                // 👇 3️⃣ Validate rating value (0 Allow kiya hai) 👇
                if (model.rating < 0 || model.rating > 5)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Rating must be between 0 and 5."
                    );
                }

                // 4️⃣ Update rating fields
                existingRating.rating = model.rating;
                existingRating.review = model.review;

                db.SaveChanges();

                // 5️⃣ Recalculate average rating for that solution
                int solutionId = existingRating.sid;

                // 👇 Sirf > 0 wali count hongi 👇
                decimal avgRating = db.UserRatings
                    .Where(r => r.sid == solutionId && r.rating > 0)
                    .Average(r => (decimal?)r.rating) ?? 0;

                avgRating = Math.Round(avgRating, 1);

                // 6️⃣ Update VPS overallrating
                var relatedVps = db.VehicleProblemSolutions
                    .Where(v => v.sid == solutionId)
                    .ToList();

                foreach (var vps in relatedVps)
                {
                    vps.overallrating = avgRating;
                }

                db.SaveChanges();

                return Request.CreateResponse(
                    HttpStatusCode.OK,
                    new
                    {
                        message = "Rating updated successfully.",
                        ratingId = ratingid,
                        newAverage = avgRating
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

        // =====================================================
        // DELETE: api/ratings/{ratingid}
        // Delete rating + recalculate VPS overallrating
        // =====================================================
        [HttpDelete]
        [Route("{ratingid:int}")]
        public HttpResponseMessage DeleteRating(int ratingid)
        {
            try
            {
                // 1️⃣ Validate id
                if (ratingid <= 0)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Valid rating id required."
                    );
                }

                // 2️⃣ Find rating
                var rating = db.UserRatings
                               .FirstOrDefault(r => r.ratingid == ratingid);

                if (rating == null)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "Rating not found."
                    );
                }

                int solutionId = rating.sid;

                // 3️⃣ Remove rating
                db.UserRatings.Remove(rating);
                db.SaveChanges();

                // 👇 4️⃣ Recalculate average (Sirf > 0 wali) 👇
                decimal avgRating = db.UserRatings
                    .Where(r => r.sid == solutionId && r.rating > 0)
                    .Average(r => (decimal?)r.rating) ?? 0;

                avgRating = Math.Round(avgRating, 1);

                // 5️⃣ Update VPS overallrating
                var relatedVps = db.VehicleProblemSolutions
                    .Where(v => v.sid == solutionId)
                    .ToList();

                foreach (var vps in relatedVps)
                {
                    vps.overallrating = avgRating;
                }

                db.SaveChanges();

                return Request.CreateResponse(
                    HttpStatusCode.OK,
                    new
                    {
                        message = "Rating deleted successfully.",
                        deletedRatingId = ratingid,
                        newAverage = avgRating
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
    }
}
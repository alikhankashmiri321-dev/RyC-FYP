using RyC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;

namespace RYC.Controllers
{
    [RoutePrefix("api/steps")]
    public class StepsController : ApiController
    {
        private readonly RYCEntities db = new RYCEntities();

        // POST: api/steps/add
        [HttpPost]
        [Route("add")]
        public IHttpActionResult AddStep()
        {
            try
            {
                var httpRequest = HttpContext.Current.Request;


                if (string.IsNullOrWhiteSpace(httpRequest.Form["sid"]))
                    return BadRequest("sid is required.");

                if (!int.TryParse(httpRequest.Form["sid"], out int sid))
                    return BadRequest("sid must be a valid integer.");

                if (string.IsNullOrWhiteSpace(httpRequest.Form["stepNo"]))
                    return BadRequest("stepNo is required.");

                if (!int.TryParse(httpRequest.Form["stepNo"], out int stepNo))
                    return BadRequest("stepNo must be a valid integer.");


                var existingStep = db.Steps
                                     .FirstOrDefault(s => s.sid == sid && s.stepNo == stepNo);

                if (existingStep != null)
                {
                    return BadRequest("Solution step already exists for this solution and step number.");
                }


                var stepDescription = httpRequest.Form["stepDescription"];
                if (string.IsNullOrWhiteSpace(stepDescription))
                    return BadRequest("stepDescription is required.");

                var file = httpRequest.Files["stepImg"];
                if (file == null || file.ContentLength == 0)
                    return BadRequest("Image is required. Please upload an image file.");

                var ext = Path.GetExtension(file.FileName).ToLower();
                var allowed = new[] { ".jpg", ".jpeg", ".png" };
                if (!allowed.Contains(ext))
                    return BadRequest("Only .jpg, .jpeg, .png image formats are allowed.");

                var vps = db.VehicleProblemSolutions
                            .FirstOrDefault(x => x.sid == sid);

                string carName = "Car";

                if (vps != null && vps.Vehicle != null)
                {
                    carName = vps.Vehicle.model;
                }

                var cleanCar = carName.Replace(" ", "_");


                var folderPath = HttpContext.Current.Server.MapPath("~/Images/Steps");
                Directory.CreateDirectory(folderPath);  

              
                var fileName = $"{cleanCar}_S{sid}_Step{stepNo}{ext}";
                
                var fullPath = Path.Combine(folderPath, fileName);

                file.SaveAs(fullPath);

                string imagePath = "/Images/Steps/" + fileName;


                var step = new Step
                {
                    sid = sid,
                    stepNo = stepNo,
                    stepDescription = stepDescription,
                    stepImg = imagePath
                };

                db.Steps.Add(step);
                db.SaveChanges();


                return Ok(new
                {
                    message = "Step added successfully.",
                    stepid = step.stepid,
                    sid = step.sid,
                    stepNo = step.stepNo,
                    description = step.stepDescription,
                    imagePath = step.stepImg
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Something went wrong: " + ex.Message));
            }
        }
        // GET: api/steps/{sid}
        [HttpGet]
        [Route("{sid:int}")]
        public HttpResponseMessage GetStepsBySolutionId(int sid)
        {
            try
            {
                if (sid <= 0)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "Valid solution id (sid) required."
                    );
                }


                var solutionExists = db.Solutions.Any(s => s.sid == sid);
                if (!solutionExists)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.NotFound,
                        "Solution not found."
                    );
                }

                
                var steps = db.Steps
                    .Where(st => st.sid == sid)
                    .OrderBy(st => st.stepNo)
                    .Select(st => new
                    {
                        stepId = st.stepid,
                        stepNo = st.stepNo,
                        description = st.stepDescription,
                        image = st.stepImg
                    })
                    .ToList();

               
                if (steps.Count == 0)
                {
                    return Request.CreateResponse(
                        HttpStatusCode.OK,
                        new
                        {
                            message = "No steps found for this solution.",
                            sid = sid,
                            total = 0,
                            steps = new List<object>()
                        }
                    );
                }


                var responseBody = new
                {
                    message = "Steps fetched successfully.",
                    sid = sid,
                    total = steps.Count,
                    steps = steps
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
        [HttpPut]
        [Route("{stepid:int}/image")]
        public IHttpActionResult UpdateStepImage(int stepid)
        {
            try
            {
                if (stepid <= 0)
                    return BadRequest("Valid stepid is required.");

                var step = db.Steps.FirstOrDefault(s => s.stepid == stepid);
                if (step == null)
                    return NotFound(); 

                var httpRequest = HttpContext.Current.Request;

              
                var file = httpRequest.Files["stepImg"];
                if (file == null || file.ContentLength == 0)
                    return BadRequest("stepImg file is required.");

                var ext = Path.GetExtension(file.FileName).ToLower();
                var allowed = new[] { ".jpg", ".jpeg", ".png" };
                if (!allowed.Contains(ext))
                    return BadRequest("Only .jpg, .jpeg, .png formats are allowed.");

                
                var newDesc = httpRequest.Form["stepDescription"];
                if (!string.IsNullOrWhiteSpace(newDesc))
                    step.stepDescription = newDesc;

                if (!string.IsNullOrWhiteSpace(httpRequest.Form["stepNo"]) &&
                    int.TryParse(httpRequest.Form["stepNo"], out int newStepNo) &&
                    newStepNo > 0)
                {
                    
                    var duplicate = db.Steps.Any(x => x.sid == step.sid
                                                  && x.stepNo == newStepNo
                                                  && x.stepid != step.stepid);
                    if (duplicate)
                        return BadRequest("This stepNo already exists for the same solution (sid).");

                    step.stepNo = newStepNo;
                }

              
                string carName = "Car";
                var vps = db.VehicleProblemSolutions.FirstOrDefault(x => x.sid == step.sid);
                if (vps != null && vps.Vehicle != null && !string.IsNullOrWhiteSpace(vps.Vehicle.model))
                    carName = vps.Vehicle.model;

                var cleanCar = carName.Replace(" ", "_");

               
                var fileName = $"{cleanCar}_S{step.sid}_Step{step.stepNo}_ID{step.stepid}{ext}";

                var folderPath = HttpContext.Current.Server.MapPath("~/Images/Steps");
                Directory.CreateDirectory(folderPath);

                var fullPath = Path.Combine(folderPath, fileName);
                file.SaveAs(fullPath);

                step.stepImg = "/Images/Steps/" + fileName;

                db.SaveChanges();

                return Ok(new
                {
                    message = "Step image updated successfully.",
                    stepid = step.stepid,
                    sid = step.sid,
                    stepNo = step.stepNo,
                    description = step.stepDescription,
                    imagePath = step.stepImg
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Something went wrong: " + ex.Message));
            }
        }

        // =====================================================
        // DELETE: api/steps/{stepid}
        // Delete a specific step
        // =====================================================
        [HttpDelete]
        [Route("{stepid:int}")]
        public IHttpActionResult DeleteStep(int stepid)
        {
            try
            {
                if (stepid <= 0)
                    return BadRequest("Valid stepid is required.");

                var step = db.Steps.FirstOrDefault(s => s.stepid == stepid);

                if (step == null)
                    return NotFound();

                // Optional: Delete image file from server
                if (!string.IsNullOrWhiteSpace(step.stepImg))
                {
                    var imagePath = HttpContext.Current.Server.MapPath(step.stepImg);
                    if (File.Exists(imagePath))
                    {
                        File.Delete(imagePath);
                    }
                }

                db.Steps.Remove(step);
                db.SaveChanges();

                return Ok(new
                {
                    message = "Step deleted successfully.",
                    deletedStepId = stepid
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Something went wrong: " + ex.Message));
            }
        }


        // =====================================================
        // PUT: api/steps/{stepid}
        // Update step description and step number
        // =====================================================
        [HttpPut]
        [Route("{stepid:int}")]
        public IHttpActionResult UpdateStep(int stepid, Step model)
        {
            try
            {
                if (stepid <= 0)
                    return BadRequest("Valid stepid is required.");

                var step = db.Steps.FirstOrDefault(s => s.stepid == stepid);

                if (step == null)
                    return NotFound();

                // Validate description
                if (string.IsNullOrWhiteSpace(model.stepDescription))
                    return BadRequest("stepDescription is required.");

                // Validate stepNo
                if (model.stepNo <= 0)
                    return BadRequest("Valid stepNo is required.");

                // Check duplicate stepNo for same solution
                bool duplicate = db.Steps.Any(s =>
                    s.sid == step.sid &&
                    s.stepNo == model.stepNo &&
                    s.stepid != step.stepid);

                if (duplicate)
                    return BadRequest("This stepNo already exists for this solution.");

                // Update fields
                step.stepDescription = model.stepDescription;
                step.stepNo = model.stepNo;

                db.SaveChanges();

                return Ok(new
                {
                    message = "Step updated successfully.",
                    stepid = step.stepid,
                    sid = step.sid,
                    stepNo = step.stepNo,
                    description = step.stepDescription
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Something went wrong: " + ex.Message));
            }
        }

    }
}
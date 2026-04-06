//using AutoMapper;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using WebApplication2.Data;
//using WebApplication2.Models;
//using WebApplication2.Models.Dto;
//using WebApplication2.Models.DTO;

//namespace WebApplication2.Controllers
//{
//    [Route("/villas")]
//    [ApiController]
//    public class VillaController : ControllerBase
//    {
//        private readonly ApplicationDbContext _db;
//        private readonly IMapper _mapper;
//        public VillaController(ApplicationDbContext db, IMapper mapper)
//        {
//            _db = db;
//            _mapper = mapper;

//        }


//        [HttpGet]
//        //[Route("/GetAllvillas")]
//        public async Task<ActionResult<IEnumerable<Villa>>> GetVillas()
//        {
//            var villas = await _db.Villa.ToListAsync();
//            //return Ok(await _db.Villa.ToListAsync());
//            return Ok(_mapper.Map<List<VillaDTO>>(villas));

//        }

//        [HttpGet("{id:int}")]
//        //[Route("/GetVillaByID")]

//        public async Task<ActionResult<Villa>> GetVillasById(int id)
//        {
//            try
//            {
//                if (id <= 0)
//                {
//                    return BadRequest("villa id must be greater than 0");
//                }

//                var villa = await _db.Villa.FirstOrDefaultAsync(u => u.Id == id);
//                if (villa == null) { return NotFound($"No villa found"); }
//                return Ok(villa);
//            }

//            catch (Exception ex)
//            {
//                return StatusCode(StatusCodes.Status500InternalServerError, $"'An err occured'");
//            }



//        }
//        [Route("/CreateVillas")]
//        [HttpPost]
//        public async Task<ActionResult<Villa>> CreateVilla(CreateVillaDTO villaDTO, int id)
//        {



//            var duplicate = await _db.Villa
//    .FirstOrDefaultAsync(u =>
//        u.Id != id &&
//        u.Name.ToLower().Trim() == villaDTO.Name.ToLower().Trim()
//    );

//            try
//            {
//                if (duplicate != null)
//                    return BadRequest("A villa with this name already exists.");
//                //if (Villa <= 0)
//                //{
//                //    return BadRequest("villa id must be greater than 0");
//                //}

//                //var villa = await _db.Villa.FirstOrDefaultAsync(u => u.Id == id);
//                if (villaDTO == null)
//                {
//                    return NotFound($"villa data is requierd");
//                }

//                Villa villa = _mapper.Map<Villa>(villaDTO);
//                //    new Villa()
//                //{

//                //    Name = villaDTO.Name,
//                //    Details = villaDTO.Details,
//                //    Rate = villaDTO.Rate,
//                //    Sqft = villaDTO.Sqft,
//                //    Occupancy = villaDTO.Occupancy,
//                //    ImgUrl = villaDTO.ImgUrl,
//                //};

//                await _db.Villa.AddAsync(villa);
//                await _db.SaveChangesAsync();
//                return
//                    //Ok(villaDTO);
//                    CreatedAtAction(nameof(CreateVilla), new { id = villa.Id }, villa);
//            }

//            catch (Exception ex)
//            {
//                return StatusCode(StatusCodes.Status500InternalServerError, $"'An err occured'");
//            }
//        }





//        //[Route("/UpdateVillas")]
//        [HttpPut("{id:int}")]
//        public async Task<ActionResult<Villa>> UpdateVilla(int id, UpdateVillaDTO villaDTO)
//        {
//            try
//            {
//                if (villaDTO == null)
//                    return BadRequest("Villa data is required.");

//                var existingVilla = await _db.Villa.FirstOrDefaultAsync(u => u.Id == id
//        );

//                if (existingVilla == null)
//                    return NotFound($"Villa with ID {id} does not exist.");

//                // Check for duplicate name
//                var duplicate = await _db.Villa
//               .FirstOrDefaultAsync(u =>
//                   u.Id != id &&
//                   u.Name.ToLower().Trim() == villaDTO.Name.ToLower().Trim()
//               );

//                if (duplicate != null)
//                    return BadRequest("A villa with this name already exists.");

//                // Map updated fields into the existing entity
//                _mapper.Map(villaDTO, existingVilla);

//                existingVilla.UpdatedDate = DateTime.Now;

//                await _db.SaveChangesAsync();

//                return NoContent();
//            }
//            catch
//            {
//                return StatusCode(StatusCodes.Status500InternalServerError,
//                    "An error occurred while updating the villa.");
//            }
//        }



//        [Route("/DeleteVillas")]
//        [HttpDelete("{id:int}")]
//        public async Task<ActionResult<Villa>> DeleteVilla(int id)
//        {
//            try
//            {
//                //if (Villa <= 0)
//                //{
//                //    return BadRequest("villa id must be greater than 0");
//                //}

//                var ExistVilla = await _db.Villa.FirstOrDefaultAsync(u => u.Id == id);
//                if (ExistVilla == null)
//                {
//                    return NotFound($"villa data is requierd");
//                }

//                _db.Villa.Remove(ExistVilla);

//                //    Villa villa = _mapper.Map<Villa>(ExistVilla);
//                //    new Villa()
//                //{

//                //    Name = villaDTO.Name,
//                //    Details = villaDTO.Details,
//                //    Rate = villaDTO.Rate,
//                //    Sqft = villaDTO.Sqft,
//                //    Occupancy = villaDTO.Occupancy,
//                //    ImgUrl = villaDTO.ImgUrl,
//                //};

//                //await _db.Villa.AddAsync(villa);
//                await _db.SaveChangesAsync();
//                return
//                    //Ok(villaDTO);
//                    NoContent();
//                //   CreatedAtAction(nameof(CreateVilla), new { id = villa.Id }, villa);
//            }

//            catch (Exception ex)
//            {
//                return StatusCode(StatusCodes.Status500InternalServerError, $"'An err occured'");
//            }
//        }



//    }
//}

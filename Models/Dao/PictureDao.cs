using ImageResizer;
using Models.EF;
using PagedList;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Models.Dao
{
    public class PictureDao
    {
        OnlineShopDbContext dbContext;
        private const int MULTIPLE_THUMB_DIRECTORIES_LENGTH = 3;

        public PictureDao()
        {
            dbContext = new OnlineShopDbContext();
        }

        public IEnumerable<Picture> GetPictures(int page, int pageSize)
        {
            IEnumerable<Picture> pictures = dbContext.Pictures;
            return pictures.OrderByDescending(x => x.Id).ToPagedList(page, pageSize);
        }

        public int InsertPicture(Picture picture)
        {
            if (picture == null)
                throw new ArgumentNullException(nameof(picture));

            dbContext.Pictures.Add(picture);
            dbContext.SaveChanges();
            return picture.Id;
        }
        public virtual Picture InsertPicture(byte[] pictureBinary, string mimeType, string seoFilename,
    string altAttribute = null, string titleAttribute = null,
    bool isNew = true, bool validateBinary = true)
        {

            var picture = new Picture
            {
                PictureBinary = pictureBinary,
                MimeType = mimeType,
                SeoFilename = seoFilename,
                AltAttribute = altAttribute,
                TitleAttribute = titleAttribute,
                IsNew = isNew,
            };
            dbContext.Pictures.Add(picture);
            dbContext.SaveChanges();
            return picture;
        }

        public virtual IList<Picture> GetPicturesByProductId(int productId, int recordsToReturn = 0)
        {
            if (productId == 0)
                return new List<Picture>();

            var query = from p in dbContext.Pictures
                        join pp in dbContext.Product_Picture_Mapping on p.Id equals pp.PictureId
                        orderby pp.DisplayOrder, pp.Id
                        where pp.ProductId == productId
                        select p;

            if (recordsToReturn > 0)
                query = query.Take(recordsToReturn);

            var pics = query.ToList();
            return pics;
        }
        public bool UpdatePicture(Picture entity)
        {
            var picture = dbContext.Pictures.Find(entity.Id);
            if (picture == null)
                throw new ArgumentNullException(nameof(picture));

            picture.PictureBinary = entity.PictureBinary;
            picture.MimeType = entity.MimeType;
            picture.SeoFilename = entity.SeoFilename;
            picture.AltAttribute = entity.AltAttribute;
            picture.TitleAttribute = entity.TitleAttribute;
            picture.IsNew = entity.IsNew;

            dbContext.SaveChanges();
            return true;
        }
        public bool DeletePicture(int pictureId)
        {
            if (pictureId == 0)
                return false;

            var picture = dbContext.Pictures.Find(pictureId);
            if (picture == null)
                throw new ArgumentNullException(nameof(picture));

            dbContext.Pictures.Remove(picture);
            dbContext.SaveChanges();
            return true;
        }

        public Picture GetPictureById(int pictureId)
        {
            if (pictureId == 0)
                return null;

            return dbContext.Pictures.Find(pictureId);
        }


        #region Utilities

        /// <summary>
        /// Calculates picture dimensions whilst maintaining aspect
        /// </summary>
        /// <param name="originalSize">The original picture size</param>
        /// <param name="targetSize">The target picture size (longest side)</param>
        /// <param name="resizeType">Resize type</param>
        /// <param name="ensureSizePositive">A value indicatingh whether we should ensure that size values are positive</param>
        /// <returns></returns>
        protected virtual Size CalculateDimensions(Size originalSize, int targetSize,
            ResizeType resizeType = ResizeType.LongestSide, bool ensureSizePositive = true)
        {
            float width, height;

            switch (resizeType)
            {
                case ResizeType.LongestSide:
                    if (originalSize.Height > originalSize.Width)
                    {
                        // portrait
                        width = originalSize.Width * (targetSize / (float)originalSize.Height);
                        height = targetSize;
                    }
                    else
                    {
                        // landscape or square
                        width = targetSize;
                        height = originalSize.Height * (targetSize / (float)originalSize.Width);
                    }
                    break;
                case ResizeType.Width:
                    width = targetSize;
                    height = originalSize.Height * (targetSize / (float)originalSize.Width);
                    break;
                case ResizeType.Height:
                    width = originalSize.Width * (targetSize / (float)originalSize.Height);
                    height = targetSize;
                    break;
                default:
                    throw new Exception("Not supported ResizeType");
            }

            if (ensureSizePositive)
            {
                if (width < 1)
                    width = 1;
                if (height < 1)
                    height = 1;
            }

            //we invoke Math.Round to ensure that no white background is rendered - https://www.nopcommerce.com/boards/t/40616/image-resizing-bug.aspx
            return new Size((int)Math.Round(width), (int)Math.Round(height));
        }

        /// <summary>
        /// Returns the file extension from mime type.
        /// </summary>
        /// <param name="mimeType">Mime type</param>
        /// <returns>File extension</returns>
        protected virtual string GetFileExtensionFromMimeType(string mimeType)
        {
            if (mimeType == null)
                return null;

            //TODO use FileExtensionContentTypeProvider to get file extension

            var parts = mimeType.Split('/');
            var lastPart = parts[parts.Length - 1];
            switch (lastPart)
            {
                case "pjpeg":
                    lastPart = "jpg";
                    break;
                case "x-png":
                    lastPart = "png";
                    break;
                case "x-icon":
                    lastPart = "ico";
                    break;
            }
            return lastPart;
        }

        /// <summary>
        /// Loads a picture from file
        /// </summary>
        /// <param name="pictureId">Picture identifier</param>
        /// <param name="mimeType">MIME type</param>
        /// <returns>Picture binary</returns>
        protected virtual byte[] LoadPictureFromFile(int pictureId, string mimeType)
        {
            var lastPart = GetFileExtensionFromMimeType(mimeType);
            var fileName = $"{pictureId:0000000}_0.{lastPart}";
            var filePath = GetPictureLocalPath(fileName);
            if (!File.Exists(filePath))
                return new byte[0];
            return File.ReadAllBytes(filePath);
        }

        /// <summary>
        /// Save picture on file system
        /// </summary>
        /// <param name="pictureId">Picture identifier</param>
        /// <param name="pictureBinary">Picture binary</param>
        /// <param name="mimeType">MIME type</param>
        protected virtual void SavePictureInFile(int pictureId, byte[] pictureBinary, string mimeType)
        {
            var lastPart = GetFileExtensionFromMimeType(mimeType);
            var fileName = $"{pictureId:0000000}_0.{lastPart}";
            File.WriteAllBytes(GetPictureLocalPath(fileName), pictureBinary);
        }

        /// <summary>
        /// Delete a picture on file system
        /// </summary>
        /// <param name="picture">Picture</param>
        protected virtual void DeletePictureOnFileSystem(Picture picture)
        {
            if (picture == null)
                throw new ArgumentNullException(nameof(picture));

            var lastPart = GetFileExtensionFromMimeType(picture.MimeType);
            var fileName = $"{picture.Id:0000000}_0.{lastPart}";
            var filePath = GetPictureLocalPath(fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        /// <summary>
        /// Delete picture thumbs
        /// </summary>
        /// <param name="picture">Picture</param>
        protected virtual void DeletePictureThumbs(Picture picture)
        {
            var filter = $"{picture.Id:0000000}*.*";
            var thumbDirectoryPath = Path.Combine(HttpContext.Current.Server.MapPath("~/App_Data"), "images\\thumbs");
            var currentFiles = System.IO.Directory.GetFiles(thumbDirectoryPath, filter, SearchOption.AllDirectories);
            foreach (var currentFileName in currentFiles)
            {
                var thumbFilePath = GetThumbLocalPath(currentFileName);
                File.Delete(thumbFilePath);
            }
        }

        /// <summary>
        /// Get picture (thumb) local path
        /// </summary>
        /// <param name="thumbFileName">Filename</param>
        /// <returns>Local picture thumb path</returns>
        protected virtual string GetThumbLocalPath(string thumbFileName)
        {
            var thumbsDirectoryPath = Path.Combine(HttpContext.Current.Server.MapPath("~/Content"), "images\\thumbs");

            //get the first two letters of the file name
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(thumbFileName);
            if (fileNameWithoutExtension != null && fileNameWithoutExtension.Length > MULTIPLE_THUMB_DIRECTORIES_LENGTH)
            {
                var subDirectoryName = fileNameWithoutExtension.Substring(0, MULTIPLE_THUMB_DIRECTORIES_LENGTH);
                thumbsDirectoryPath = Path.Combine(thumbsDirectoryPath, subDirectoryName);
                if (!System.IO.Directory.Exists(thumbsDirectoryPath))
                {
                    System.IO.Directory.CreateDirectory(thumbsDirectoryPath);
                }
            }
            var thumbFilePath = Path.Combine(thumbsDirectoryPath, thumbFileName);
            return thumbFilePath;
        }

        /// <summary>
        /// Get picture (thumb) URL 
        /// </summary>
        /// <param name="thumbFileName">Filename</param>
        /// <param name="storeLocation">Store location URL; null to use determine the current store location automatically</param>
        /// <returns>Local picture thumb path</returns>
        protected virtual string GetThumbUrl(string thumbFileName, string storeLocation = null)
        {
            storeLocation = HttpRuntime.AppDomainAppVirtualPath;
            var url = storeLocation + "Content/images/thumbs/";


            //get the first two letters of the file name
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(thumbFileName);
            if (fileNameWithoutExtension != null && fileNameWithoutExtension.Length > MULTIPLE_THUMB_DIRECTORIES_LENGTH)
            {
                var subDirectoryName = fileNameWithoutExtension.Substring(0, MULTIPLE_THUMB_DIRECTORIES_LENGTH);
                url = url + subDirectoryName + "/";
            }

            url = url + thumbFileName;
            return url;
        }

        /// <summary>
        /// Get picture local path. Used when images stored on file system (not in the database)
        /// </summary>
        /// <param name="fileName">Filename</param>
        /// <returns>Local picture path</returns>
        protected virtual string GetPictureLocalPath(string fileName)
        {
            return Path.Combine(HttpContext.Current.Server.MapPath("~/Content"), "images", fileName);
        }

        /// <summary>
        /// Gets the loaded picture binary depending on picture storage settings
        /// </summary>
        /// <param name="picture">Picture</param>
        /// <param name="fromDb">Load from database; otherwise, from file system</param>
        /// <returns>Picture binary</returns>
        protected virtual byte[] LoadPictureBinary(Picture picture, bool fromDb)
        {
            if (picture == null)
                throw new ArgumentNullException(nameof(picture));

            var result = fromDb
                ? picture.PictureBinary
                : LoadPictureFromFile(picture.Id, picture.MimeType);
            return result;
        }

        /// <summary>
        /// Get a value indicating whether some file (thumb) already exists
        /// </summary>
        /// <param name="thumbFilePath">Thumb file path</param>
        /// <param name="thumbFileName">Thumb file name</param>
        /// <returns>Result</returns>
        protected virtual bool GeneratedThumbExists(string thumbFilePath, string thumbFileName)
        {
            return File.Exists(thumbFilePath);
        }

        /// <summary>
        /// Save a value indicating whether some file (thumb) already exists
        /// </summary>
        /// <param name="thumbFilePath">Thumb file path</param>
        /// <param name="thumbFileName">Thumb file name</param>
        /// <param name="mimeType">MIME type</param>
        /// <param name="binary">Picture binary</param>
        protected virtual void SaveThumb(string thumbFilePath, string thumbFileName, string mimeType, byte[] binary)
        {
            //ensure \thumb directory exists
            var thumbsDirectoryPath = Path.Combine(HttpContext.Current.Server.MapPath("~/Content"), "images\\thumbs");
            if (!System.IO.Directory.Exists(thumbsDirectoryPath))
                System.IO.Directory.CreateDirectory(thumbsDirectoryPath);

            //save
            File.WriteAllBytes(thumbFilePath, binary);
        }

        #endregion

        #region Getting picture local path/URL methods

        /// <summary>
        /// Gets the loaded picture binary depending on picture storage settings
        /// </summary>
        /// <param name="picture">Picture</param>
        /// <returns>Picture binary</returns>
        public virtual byte[] LoadPictureBinary(Picture picture)
        {
            return LoadPictureBinary(picture, true);
        }

        /// <summary>
        /// Gets the default picture URL
        /// </summary>
        /// <param name="targetSize">The target picture size (longest side)</param>
        /// <param name="defaultPictureType">Default picture type</param>
        /// <param name="storeLocation">Store location URL; null to use determine the current store location automatically</param>
        /// <returns>Picture URL</returns>
        public virtual string GetDefaultPictureUrl(int targetSize = 0,
            PictureType defaultPictureType = PictureType.Entity,
            string storeLocation = null)
        {
            string defaultImageFileName;
            switch (defaultPictureType)
            {
                case PictureType.Avatar:
                    defaultImageFileName = "default-avatar.jpg";
                    break;
                case PictureType.Entity:
                default:
                    defaultImageFileName = "default-image.png";
                    break;
            }
            var filePath = GetPictureLocalPath(defaultImageFileName);
            if (!File.Exists(filePath))
            {
                return "";
            }

            if (targetSize == 0)
            {
                var url = HttpRuntime.AppDomainAppVirtualPath + "images/" + defaultImageFileName;
                return url;
            }
            else
            {
                var fileExtension = Path.GetExtension(filePath);
                var thumbFileName = $"{Path.GetFileNameWithoutExtension(filePath)}_{targetSize}{fileExtension}";
                var thumbFilePath = GetThumbLocalPath(thumbFileName);
                if (!GeneratedThumbExists(thumbFilePath, thumbFileName))
                {
                    using (var b = new Bitmap(filePath))
                    {
                        using (var destStream = new MemoryStream())
                        {
                            var newSize = CalculateDimensions(b.Size, targetSize);
                            ImageBuilder.Current.Build(b, destStream, new ResizeSettings
                            {
                                Width = newSize.Width,
                                Height = newSize.Height,
                                Scale = ScaleMode.Both,
                                Quality = 100
                            });
                            var destBinary = destStream.ToArray();
                            SaveThumb(thumbFilePath, thumbFileName, "", destBinary);
                        }
                    }
                }
                var url = GetThumbUrl(thumbFileName, storeLocation);
                return url;
            }
        }

        /// <summary>
        /// Get a picture URL
        /// </summary>
        /// <param name="pictureId">Picture identifier</param>
        /// <param name="targetSize">The target picture size (longest side)</param>
        /// <param name="showDefaultPicture">A value indicating whether the default picture is shown</param>
        /// <param name="storeLocation">Store location URL; null to use determine the current store location automatically</param>
        /// <param name="defaultPictureType">Default picture type</param>
        /// <returns>Picture URL</returns>
        public virtual string GetPictureUrl(int pictureId,
            int targetSize = 0,
            bool showDefaultPicture = true,
            string storeLocation = null,
            PictureType defaultPictureType = PictureType.Entity)
        {
            var picture = GetPictureById(pictureId);
            return GetPictureUrl(picture, targetSize, showDefaultPicture, storeLocation, defaultPictureType);
        }

        /// <summary>
        /// Get a picture URL
        /// </summary>
        /// <param name="picture">Picture instance</param>
        /// <param name="targetSize">The target picture size (longest side)</param>
        /// <param name="showDefaultPicture">A value indicating whether the default picture is shown</param>
        /// <param name="storeLocation">Store location URL; null to use determine the current store location automatically</param>
        /// <param name="defaultPictureType">Default picture type</param>
        /// <returns>Picture URL</returns>
        public virtual string GetPictureUrl(Picture picture,
            int targetSize = 0,
            bool showDefaultPicture = true,
            string storeLocation = null,
            PictureType defaultPictureType = PictureType.Entity)
        {
            var url = string.Empty;
            byte[] pictureBinary = null;
            if (picture != null)
                pictureBinary = LoadPictureBinary(picture);
            if (picture == null || pictureBinary == null || pictureBinary.Length == 0)
            {
                if (showDefaultPicture)
                {
                    url = GetDefaultPictureUrl(targetSize, defaultPictureType, storeLocation);
                }
                return url;
            }

            //if (picture.IsNew)
            //{
            //    DeletePictureThumbs(picture);
            //}

            var seoFileName = picture.SeoFilename; // = GetPictureSeName(picture.SeoFilename); //just for sure

            var lastPart = GetFileExtensionFromMimeType(picture.MimeType);
            string thumbFileName;
            if (targetSize == 0)
            {
                thumbFileName = !string.IsNullOrEmpty(seoFileName)
                    ? $"{picture.Id:0000000}_{seoFileName}.{lastPart}"
                    : $"{picture.Id:0000000}.{lastPart}";
            }
            else
            {
                thumbFileName = !string.IsNullOrEmpty(seoFileName)
                    ? $"{picture.Id:0000000}_{seoFileName}_{targetSize}.{lastPart}"
                    : $"{picture.Id:0000000}_{targetSize}.{lastPart}";
            }
            var thumbFilePath = GetThumbLocalPath(thumbFileName);

            //the named mutex helps to avoid creating the same files in different threads,
            //and does not decrease performance significantly, because the code is blocked only for the specific file.
            using (var mutex = new Mutex(false, thumbFileName))
            {
                if (!GeneratedThumbExists(thumbFilePath, thumbFileName))
                {
                    mutex.WaitOne();

                    //check, if the file was created, while we were waiting for the release of the mutex.
                    if (!GeneratedThumbExists(thumbFilePath, thumbFileName))
                    {
                        byte[] pictureBinaryResized;

                        //resizing required
                        if (targetSize != 0)
                        {
                            using (var stream = new MemoryStream(pictureBinary))
                            {
                                Bitmap b = null;
                                try
                                {
                                    //try-catch to ensure that picture binary is really OK. Otherwise, we can get "Parameter is not valid" exception if binary is corrupted for some reasons
                                    b = new Bitmap(stream);
                                }
                                catch (ArgumentException exc)
                                {
                                }

                                if (b == null)
                                {
                                    //bitmap could not be loaded for some reasons
                                    return url;
                                }

                                using (var destStream = new MemoryStream())
                                {
                                    var newSize = CalculateDimensions(b.Size, targetSize);
                                    ImageBuilder.Current.Build(b, destStream, new ResizeSettings
                                    {
                                        Width = newSize.Width,
                                        Height = newSize.Height,
                                        Scale = ScaleMode.Both,
                                        Quality = 100
                                    });
                                    pictureBinaryResized = destStream.ToArray();
                                    b.Dispose();
                                }
                            }
                        }
                        else
                        {
                            //create a copy of pictureBinary
                            pictureBinaryResized = pictureBinary.ToArray();
                        }

                        SaveThumb(thumbFilePath, thumbFileName, picture.MimeType, pictureBinaryResized);
                    }

                    mutex.ReleaseMutex();
                }

            }
            url = GetThumbUrl(thumbFileName, storeLocation);
            return url;
        }

        /// <summary>
        /// Get a picture local path
        /// </summary>
        /// <param name="picture">Picture instance</param>
        /// <param name="targetSize">The target picture size (longest side)</param>
        /// <param name="showDefaultPicture">A value indicating whether the default picture is shown</param>
        /// <returns></returns>
        public virtual string GetThumbLocalPath(Picture picture, int targetSize = 0, bool showDefaultPicture = true)
        {
            var url = GetPictureUrl(picture, targetSize, showDefaultPicture);
            if (string.IsNullOrEmpty(url))
                return string.Empty;

            return GetThumbLocalPath(Path.GetFileName(url));
        }

        #endregion
    }
}

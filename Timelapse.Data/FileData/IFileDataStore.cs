namespace Timelapse.Data.FileData
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Store that manages file related data.
    /// </summary>
    public interface IFileDataStore
    {
        /// <summary>
        /// Inserts the specified files into the data store.
        /// </summary>
        /// <param name="models">The models to insert into the data store.</param>
        /// <returns>The number of records inserted.</returns>
        int InsertFiles(List<FileModel> models);

        /// <summary>
        /// Inserts the specified files into the data store.
        /// </summary>
        /// <param name="models">The models to insert into the data store.</param>
        /// <param name="cancellationToken">The token used to track task cancellation.</param>
        /// <returns>A <see cref="Task"/>.</returns>
        Task<int> InsertFilesAsync(List<FileModel> models, CancellationToken cancellationToken = default(CancellationToken));
    }
}
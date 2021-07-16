// -----------------------------------------------------------------------
// <copyright file="IFileFormat.cs" company="">
// Triangle.NET code by Christian Woltering, http://triangle.codeplex.com/
// </copyright>
// -----------------------------------------------------------------------

namespace PowerStorage.Geometry.IO
{
    public interface IFileFormat
    {
        bool IsSupported(string file);
    }
}

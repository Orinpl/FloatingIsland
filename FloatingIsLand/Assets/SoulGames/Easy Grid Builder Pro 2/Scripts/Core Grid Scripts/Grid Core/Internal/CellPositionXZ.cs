namespace SoulGames.EasyGridBuilderPro
{
    /// <summary>
    /// Represents a position in a grid using x and z coordinates.
    /// </summary>
    public struct CellPositionXZ
    {
        public int x;
        public int z;
        
        /// <summary>
        /// Initializes a new instance of the CellPositionXZ struct.
        /// </summary>
        /// <param name="x">The x-coordinate of the position.</param>
        /// <param name="z">The z-coordinate of the position.</param>
        public CellPositionXZ (int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        #region Operator Overrides Start:
        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        public override string ToString()
        {
            return $"[{x}, {z}]";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is CellPositionXZ other && Equals(other);
        }

        /// <summary>
        /// Determines whether the specified CellPositionXZ is equal to the current CellPositionXZ.
        /// </summary>
        public bool Equals(CellPositionXZ other)
        {
            return x == other.x && z == other.z;
        }

        /// <summary>
        /// Returns a hash code for the current object.
        /// </summary>
        public override int GetHashCode()
        {
            return x.GetHashCode() ^ z.GetHashCode();
        }

        /// <summary>
        /// Checks if two CellPositionXZ instances are equal.
        /// </summary>
        public static bool operator ==(CellPositionXZ left, CellPositionXZ right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Checks if two CellPositionXZ instances are not equal.
        /// </summary>
        public static bool operator !=(CellPositionXZ left, CellPositionXZ right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Adds two CellPositionXZ instances.
        /// </summary>
        public static CellPositionXZ operator +(CellPositionXZ a, CellPositionXZ b)
        {
            return new CellPositionXZ(a.x + b.x, a.z + b.z);
        } 

        /// <summary>
        /// Subtracts one CellPositionXZ instance from another.
        /// </summary>
        public static CellPositionXZ operator -(CellPositionXZ a, CellPositionXZ b)
        {
            return new CellPositionXZ(a.x - b.x, a.z - b.z);
        }

        /// <summary>
        /// Multiplies two CellPositionXZ instances.
        /// </summary>
        public static CellPositionXZ operator *(CellPositionXZ a, CellPositionXZ b)
        {
            return new CellPositionXZ(a.x * b.x, a.z * b.z);
        }

        /// <summary>
        /// Multiplies a CellPositionXZ instance with an integer multiplier.
        /// </summary>
        public static CellPositionXZ operator *(CellPositionXZ a, int multiplier)
        {
            return new CellPositionXZ(a.x * multiplier, a.z * multiplier);
        }

        /// <summary>
        /// Divides one CellPositionXZ instance by another.
        /// </summary>
        public static CellPositionXZ operator /(CellPositionXZ a, CellPositionXZ b)
        {
            return new CellPositionXZ(a.x / b.x, a.z / b.z);
        }

        /// <summary>
        /// Divides a CellPositionXZ instance by an integer divisor.
        /// </summary>
        public static CellPositionXZ operator /(CellPositionXZ a, int divisor)
        {
            return new CellPositionXZ(a.x / divisor, a.z / divisor);
        }
        #endregion Operator Overrides End:
    }
}
namespace SoulGames.EasyGridBuilderPro
{
    /// <summary>
    /// Represents a position in a grid using x and y coordinates.
    /// </summary>
    public struct CellPositionXY
    {
        public int x;
        public int y;
        
        /// <summary>
        /// Initializes a new instance of the CellPositionXY struct.
        /// </summary>
        /// <param name="x">The x-coordinate of the position.</param>
        /// <param name="y">The y-coordinate of the position.</param>
        public CellPositionXY (int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        #region Operator Overrides Start:
        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        public override string ToString()
        {
            return $"[{x}, {y}]";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is CellPositionXY other && Equals(other);
        }

        /// <summary>
        /// Determines whether the specified CellPositionXY is equal to the current CellPositionXY.
        /// </summary>
        public bool Equals(CellPositionXY other)
        {
            return x == other.x && y == other.y;
        }

        /// <summary>
        /// Returns a hash code for the current object.
        /// </summary>
        public override int GetHashCode()
        {
            return x.GetHashCode() ^ y.GetHashCode();
        }

        /// <summary>
        /// Checks if two CellPositionXY instances are equal.
        /// </summary>
        public static bool operator ==(CellPositionXY left, CellPositionXY right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Checks if two CellPositionXY instances are not equal.
        /// </summary>
        public static bool operator !=(CellPositionXY left, CellPositionXY right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Adds two CellPositionXY instances.
        /// </summary>
        public static CellPositionXY operator +(CellPositionXY a, CellPositionXY b)
        {
            return new CellPositionXY(a.x + b.x, a.y + b.y);
        } 

        /// <summary>
        /// Subtracts one CellPositionXY instance from another.
        /// </summary>
        public static CellPositionXY operator -(CellPositionXY a, CellPositionXY b)
        {
            return new CellPositionXY(a.x - b.x, a.y - b.y);
        }

        /// <summary>
        /// Multiplies two CellPositionXY instances.
        /// </summary>
        public static CellPositionXY operator *(CellPositionXY a, CellPositionXY b)
        {
            return new CellPositionXY(a.x * b.x, a.y * b.y);
        }

        /// <summary>
        /// Multiplies a CellPositionXY instance with an integer multiplier.
        /// </summary>
        public static CellPositionXY operator *(CellPositionXY a, int multiplier)
        {
            return new CellPositionXY(a.x * multiplier, a.y * multiplier);
        }

        /// <summary>
        /// Divides one CellPositionXY instance by another.
        /// </summary>
        public static CellPositionXY operator /(CellPositionXY a, CellPositionXY b)
        {
            return new CellPositionXY(a.x / b.x, a.y / b.y);
        }

        /// <summary>
        /// Divides a CellPositionXY instance by an integer divisor.
        /// </summary>
        public static CellPositionXY operator /(CellPositionXY a, int divisor)
        {
            return new CellPositionXY(a.x / divisor, a.y / divisor);
        }
        #endregion Operator Overrides End:
    }
}
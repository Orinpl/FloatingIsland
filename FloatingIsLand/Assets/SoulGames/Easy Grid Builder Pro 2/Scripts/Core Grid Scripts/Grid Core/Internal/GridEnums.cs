namespace SoulGames.EasyGridBuilderPro
{
        public enum BuildableObjectType
        {
                BuildableGridObject,
                BuildableEdgeObject,
                BuildableCornerObject,
                BuildableFreeObject,
        }

        public enum CameraMode
        {
                TopDown,
                ThirdPerson,
        }

        public enum GridAxis
        {
                XZ,
                XY,
        }

        public enum GridOrigin
        {
                Center,
                Default,
        }

        public enum GridMode            //Enums for Grid Mode
        {
                None,                   // Mode not set
                BuildMode,              // Mode for building
                DestroyMode,            // Mode for destruction
                SelectMode,             // Mode for selection
                MoveMode,               // Mode for moving
        }

        public enum MouseInputs         //Enums for Grid Axis
        {
                LeftButton,             //Left mouse button
                RightButton,            //Right mouse button
                MiddleButton,           //Middle mouse button
                None,                   //No mouse button
        }

        public enum Vector3Axis
        {
                X,
                Y,
                Z,
        }

        public enum FourDirectionalRotation           //Enums for Buildable Object rotation Direction
        {
                North,
                East,
                South,
                West,
        }

        public enum EightDirectionalRotation          //Enums for Buildable Object rotation Direction
        {
                North,
                NorthEast,
                East,
                SouthEast,
                South,
                SouthWest,
                West,
                NorthWest,
        }

        public enum GridObjectPlacementType
        {
                SinglePlacement,
                PaintPlacement,
                BoxPlacement,
                WireBoxPlacement,
                FourDirectionWirePlacement,
                LShapedPlacement,
        }

        public enum CornerObjectPlacementType
        {
                SinglePlacement,
                PaintPlacement,
                BoxPlacement,
                WireBoxPlacement,
                FourDirectionWirePlacement,
                LShapedPlacement,
        }

        public enum EdgeObjectPlacementType
        {
                SinglePlacement,
                PaintPlacement,
                WireBoxPlacement,
                FourDirectionWirePlacement,
                LShapedPlacement,
        }

        public enum FreeObjectPlacementType
        {
                SinglePlacement,
                PaintPlacement,
                SplinePlacement,
        }

        public enum CornerObjectRotationType
        {
                FourDirectionalRotation,
                EightDirectionalRotation,
                FreeRotation,
        }

        public enum FreeObjectRotationType
        {
                FourDirectionalRotation,
                EightDirectionalRotation,
                FreeRotation,
        }

        public enum CornerObjectCellDirection
        {
                NorthEast,
                SouthEast,
                SouthWest,
                NorthWest,
        }

        public enum EdgeObjectCellDirection
        {
                North,
                East,
                South,
                West,
        }

        public enum SplineTangetMode
        {
                AutoSmooth,
                Linear,
        }

        public enum SelectionMode
        {
                Individual,
                IndividualAndArea,
        }

        public enum DestructableBuildableObjectType
        {
                BuildableGridObject,
                BuildableEdgeObject,
                BuildableCornerObject,
                BuildableFreeObject,
                All,
        }

        public enum DestroyMode
        {
                Individual,
                IndividualAndArea,
        }

        public enum SelectableBuildableObjectType
        {
                BuildableGridObject,
                BuildableEdgeObject,
                BuildableCornerObject,
                BuildableFreeObject,
                All,
        }

        public enum MovableBuildableObjectType
        {
                BuildableGridObject,
                BuildableEdgeObject,
                BuildableCornerObject,
                BuildableFreeObject,
                All,
        }

        public enum IsAttachedToABuildableObject
        {
                Yes,
                No,
        }
}

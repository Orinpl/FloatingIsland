using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

namespace SoulGames.Utilities
{
    public class CustomGizmosUtilities
    {
        public static void DrawAAPolyLine(Vector3 startPos, Vector3 endPos, float thickness, Color color)
        {
            Handles.color = color;
            Handles.DrawAAPolyLine(thickness, startPos, endPos);
        }

        public static void DrawAAPolyWireBox(Vector3 center, float size, Quaternion rotation, float thickness, Color color)
        {
            float halfSize = size * 0.5f;

            // Define the 4 corners of the box in 2D (XY plane) before rotation
            Vector3[] vertices = new Vector3[4];
            vertices[0] = new Vector3(-halfSize, -halfSize, 0);
            vertices[1] = new Vector3(halfSize, -halfSize, 0);
            vertices[2] = new Vector3(halfSize, halfSize, 0);
            vertices[3] = new Vector3(-halfSize, halfSize, 0);

            // Apply rotation to each corner
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = rotation * vertices[i] + center;
            }

            Handles.color = color;

            // Draw the box
            Handles.DrawAAPolyLine(thickness, vertices[0], vertices[1]);
            Handles.DrawAAPolyLine(thickness, vertices[1], vertices[2]);
            Handles.DrawAAPolyLine(thickness, vertices[2], vertices[3]);
            Handles.DrawAAPolyLine(thickness, vertices[3], vertices[0]);
        }

        public static void DrawAAPolyWireBox(Vector3 center, Vector2 size, Quaternion rotation, float thickness, Color color)
        {
            Vector3 halfSize = new Vector3(size.x * 0.5f, size.y * 0.5f, 0);
            Vector3[] vertices = new Vector3[4];

            // Define the 4 corners of the box in 2D (XY plane) before rotation
            vertices[0] = new Vector3(-halfSize.x, -halfSize.y, 0);
            vertices[1] = new Vector3(halfSize.x, -halfSize.y, 0);
            vertices[2] = new Vector3(halfSize.x, halfSize.y, 0);
            vertices[3] = new Vector3(-halfSize.x, halfSize.y, 0);

            // Apply rotation to each corner
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = rotation * vertices[i] + center;
            }

            Handles.color = color;

            // Draw the box
            Handles.DrawAAPolyLine(thickness, vertices[0], vertices[1]);
            Handles.DrawAAPolyLine(thickness, vertices[1], vertices[2]);
            Handles.DrawAAPolyLine(thickness, vertices[2], vertices[3]);
            Handles.DrawAAPolyLine(thickness, vertices[3], vertices[0]);
        }

        public static void DrawAAPolyFilledWireBox(Vector3 center, float size, Quaternion rotation, int steps, float thickness, Color borderColor, Color lineColor)
        {
            float halfSize = size * 0.5f;
            Vector3[] vertices = new Vector3[4];

            // Define the 4 corners of the box in 2D (XY plane) before rotation
            vertices[0] = new Vector3(-halfSize, -halfSize, 0);
            vertices[1] = new Vector3(halfSize, -halfSize, 0);
            vertices[2] = new Vector3(halfSize, halfSize, 0);
            vertices[3] = new Vector3(-halfSize, halfSize, 0);

            // Apply rotation to each corner
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = rotation * vertices[i] + center;
            }

            Handles.color = borderColor;

            // Draw the box border
            Handles.DrawAAPolyLine(thickness, vertices[0], vertices[1]);
            Handles.DrawAAPolyLine(thickness, vertices[1], vertices[2]);
            Handles.DrawAAPolyLine(thickness, vertices[2], vertices[3]);
            Handles.DrawAAPolyLine(thickness, vertices[3], vertices[0]);

            Handles.color = lineColor;

            // Draw diagonal lines filling the box at 45 degrees
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 start1 = Vector3.Lerp(vertices[0], vertices[3], t);
                Vector3 end1 = Vector3.Lerp(vertices[1], vertices[2], t);
                Handles.DrawAAPolyLine(thickness, start1, end1);

                Vector3 start2 = Vector3.Lerp(vertices[3], vertices[2], t);
                Vector3 end2 = Vector3.Lerp(vertices[0], vertices[1], t);
                Handles.DrawAAPolyLine(thickness, start2, end2);
            }
        }

        public static void DrawAAPolyFilledWireBox(Vector3 center, Vector2 size, Quaternion rotation, int steps, float thickness, Color borderColor, Color lineColor)
        {
            Vector3 halfSize = new Vector3(size.x * 0.5f, size.y * 0.5f, 0);
            Vector3[] vertices = new Vector3[4];

            // Define the 4 corners of the box in 2D (XY plane) before rotation
            vertices[0] = new Vector3(-halfSize.x, -halfSize.y, 0);
            vertices[1] = new Vector3(halfSize.x, -halfSize.y, 0);
            vertices[2] = new Vector3(halfSize.x, halfSize.y, 0);
            vertices[3] = new Vector3(-halfSize.x, halfSize.y, 0);

            // Apply rotation to each corner
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = rotation * vertices[i] + center;
            }

            Handles.color = borderColor;

            // Draw the box border
            Handles.DrawAAPolyLine(thickness, vertices[0], vertices[1]);
            Handles.DrawAAPolyLine(thickness, vertices[1], vertices[2]);
            Handles.DrawAAPolyLine(thickness, vertices[2], vertices[3]);
            Handles.DrawAAPolyLine(thickness, vertices[3], vertices[0]);

            Handles.color = lineColor;

            // Draw diagonal lines filling the box at 45 degrees
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 start1 = Vector3.Lerp(vertices[0], vertices[3], t);
                Vector3 end1 = Vector3.Lerp(vertices[1], vertices[2], t);
                Handles.DrawAAPolyLine(thickness, start1, end1);

                Vector3 start2 = Vector3.Lerp(vertices[3], vertices[2], t);
                Vector3 end2 = Vector3.Lerp(vertices[0], vertices[1], t);
                Handles.DrawAAPolyLine(thickness, start2, end2);
            }
        }

        public static void DrawAAWirePolygon(Vector2 center, Vector2 size, int segments, float thickness, Color color)
        {
            Vector2 halfSize = size * 0.5f;

            // Define the 4 corners of the box
            Vector3[] corners = new Vector3[4];
            corners[0] = new Vector3(center.x - halfSize.x, center.y - halfSize.y);
            corners[1] = new Vector3(center.x + halfSize.x, center.y - halfSize.y);
            corners[2] = new Vector3(center.x + halfSize.x, center.y + halfSize.y);
            corners[3] = new Vector3(center.x - halfSize.x, center.y + halfSize.y);

            Handles.color = color;

            // Draw the box with segments
            for (int i = 0; i < 4; i++)
            {
                Vector3 start = corners[i];
                Vector3 end = corners[(i + 1) % 4];
                
                for (int j = 0; j < segments; j++)
                {
                    float t1 = (float)j / segments;
                    float t2 = (float)(j + 1) / segments;
                    Vector3 point1 = Vector3.Lerp(start, end, t1);
                    Vector3 point2 = Vector3.Lerp(start, end, t2);
                    Handles.DrawAAPolyLine(thickness, point1, point2);
                }
            }
        }

        public static void DrawAAPolyWireCube(Vector3 center, Vector3 size, Quaternion rotation, float thickness, Color color)
        {
            Vector3[] vertices = new Vector3[8];
            Vector3 halfSize = size * 0.5f;

            // Define the 8 vertices of the cube
            vertices[0] = center + rotation * new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
            vertices[1] = center + rotation * new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
            vertices[2] = center + rotation * new Vector3(halfSize.x, -halfSize.y, halfSize.z);
            vertices[3] = center + rotation * new Vector3(-halfSize.x, -halfSize.y, halfSize.z);
            vertices[4] = center + rotation * new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
            vertices[5] = center + rotation * new Vector3(halfSize.x, halfSize.y, -halfSize.z);
            vertices[6] = center + rotation * new Vector3(halfSize.x, halfSize.y, halfSize.z);
            vertices[7] = center + rotation * new Vector3(-halfSize.x, halfSize.y, halfSize.z);

            Handles.color = color;

            // Bottom square
            Handles.DrawAAPolyLine(thickness, vertices[0], vertices[1]);
            Handles.DrawAAPolyLine(thickness, vertices[1], vertices[2]);
            Handles.DrawAAPolyLine(thickness, vertices[2], vertices[3]);
            Handles.DrawAAPolyLine(thickness, vertices[3], vertices[0]);

            // Top square
            Handles.DrawAAPolyLine(thickness, vertices[4], vertices[5]);
            Handles.DrawAAPolyLine(thickness, vertices[5], vertices[6]);
            Handles.DrawAAPolyLine(thickness, vertices[6], vertices[7]);
            Handles.DrawAAPolyLine(thickness, vertices[7], vertices[4]);

            // Vertical lines
            Handles.DrawAAPolyLine(thickness, vertices[0], vertices[4]);
            Handles.DrawAAPolyLine(thickness, vertices[1], vertices[5]);
            Handles.DrawAAPolyLine(thickness, vertices[2], vertices[6]);
            Handles.DrawAAPolyLine(thickness, vertices[3], vertices[7]);
        }

        public static void DrawAAPolyWireCube(Vector3 startPos, Vector3 endPos, float size, float thickness, Color color)
        {
            Vector3 direction = (endPos - startPos).normalized;
            float distance = Vector3.Distance(startPos, endPos);
            Quaternion rotation = Quaternion.LookRotation(direction);
            Vector3 center = (startPos + endPos) / 2;

            Vector3 halfSize = new Vector3(size, size, distance) * 0.5f;

            Vector3[] vertices = new Vector3[8];

            // Define the 8 vertices of the cube
            vertices[0] = center + rotation * new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
            vertices[1] = center + rotation * new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
            vertices[2] = center + rotation * new Vector3(halfSize.x, -halfSize.y, halfSize.z);
            vertices[3] = center + rotation * new Vector3(-halfSize.x, -halfSize.y, halfSize.z);
            vertices[4] = center + rotation * new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
            vertices[5] = center + rotation * new Vector3(halfSize.x, halfSize.y, -halfSize.z);
            vertices[6] = center + rotation * new Vector3(halfSize.x, halfSize.y, halfSize.z);
            vertices[7] = center + rotation * new Vector3(-halfSize.x, halfSize.y, halfSize.z);

            Handles.color = color;

            // Bottom square
            Handles.DrawAAPolyLine(thickness, vertices[0], vertices[1]);
            Handles.DrawAAPolyLine(thickness, vertices[1], vertices[2]);
            Handles.DrawAAPolyLine(thickness, vertices[2], vertices[3]);
            Handles.DrawAAPolyLine(thickness, vertices[3], vertices[0]);

            // Top square
            Handles.DrawAAPolyLine(thickness, vertices[4], vertices[5]);
            Handles.DrawAAPolyLine(thickness, vertices[5], vertices[6]);
            Handles.DrawAAPolyLine(thickness, vertices[6], vertices[7]);
            Handles.DrawAAPolyLine(thickness, vertices[7], vertices[4]);

            // Vertical lines
            Handles.DrawAAPolyLine(thickness, vertices[0], vertices[4]);
            Handles.DrawAAPolyLine(thickness, vertices[1], vertices[5]);
            Handles.DrawAAPolyLine(thickness, vertices[2], vertices[6]);
            Handles.DrawAAPolyLine(thickness, vertices[3], vertices[7]);
        }

        public static void DrawAAPolyCircle(Vector3 center, Quaternion rotation, float radius, int segments, bool isDotted, float thickness, Color color)
        {
            Handles.color = color;
            Vector3[] circlePoints = new Vector3[segments + 1];

            // Calculate the points along the circle
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 point = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
                circlePoints[i] = center + rotation * point;
            }

            // Draw the circle or dotted circle
            for (int i = 0; i < segments; i++)
            {
                if (isDotted)
                {
                    // Draw every other segment for a dotted effect
                    if (i % 2 == 0)
                    {
                        Handles.DrawAAPolyLine(thickness, circlePoints[i], circlePoints[i + 1]);
                    }
                }
                else
                {
                    Handles.DrawAAPolyLine(thickness, circlePoints[i], circlePoints[i + 1]);
                }
            }
        }

        public static void DrawAAPolyQuarterCircle(Vector3 center, Quaternion rotation, float radius, int segments, bool isDotted, float thickness, Color color)
        {
            Handles.color = color;
            Vector3[] arcPoints = new Vector3[segments + 1];

            // Calculate the points along the quarter circle
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(0, Mathf.PI / 2, (float)i / segments); // 0 to 90 degrees (0 to PI/2 radians)
                Vector3 point = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
                arcPoints[i] = center + rotation * point;
            }

            // Draw the quarter circle or dotted quarter circle
            for (int i = 0; i < segments; i++)
            {
                if (isDotted)
                {
                    // Draw every other segment for a dotted effect
                    if (i % 2 == 0)
                    {
                        Handles.DrawAAPolyLine(thickness, arcPoints[i], arcPoints[i + 1]);
                    }
                }
                else
                {
                    Handles.DrawAAPolyLine(thickness, arcPoints[i], arcPoints[i + 1]);
                }
            }
        }

        public static void DrawAAPolyWireCylinder(Vector3 center, Vector3 direction, float radius, float height, int segments, float thickness, Color color)
        {
            Vector3[] topCircle = new Vector3[segments];
            Vector3[] bottomCircle = new Vector3[segments];

            Quaternion rotation = Quaternion.LookRotation(direction);
            Vector3 offset = direction.normalized * height * 0.5f;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 unitCircle = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
                topCircle[i] = center + offset + rotation * unitCircle;
                bottomCircle[i] = center - offset + rotation * unitCircle;
            }

            Handles.color = color;

            // Draw top circle
            for (int i = 0; i < segments; i++)
            {
                int nextIndex = (i + 1) % segments;
                Handles.DrawAAPolyLine(thickness, topCircle[i], topCircle[nextIndex]);
            }

            // Draw bottom circle
            for (int i = 0; i < segments; i++)
            {
                int nextIndex = (i + 1) % segments;
                Handles.DrawAAPolyLine(thickness, bottomCircle[i], bottomCircle[nextIndex]);
            }

            // Draw sides
            for (int i = 0; i < segments; i++)
            {
                Handles.DrawAAPolyLine(thickness, topCircle[i], bottomCircle[i]);
            }
        }

        public static void DrawAAPolyWireCylinder(Vector3 startPos, Vector3 endPos, float radius, int segments, float thickness, Color color)
        {
            Vector3 direction = endPos - startPos;
            float height = direction.magnitude;
            Vector3 center = (startPos + endPos) / 2;
            Vector3 up = direction.normalized;

            Vector3[] topCircle = new Vector3[segments];
            Vector3[] bottomCircle = new Vector3[segments];

            Quaternion rotation = Quaternion.LookRotation(up);
            Vector3 offset = up * height * 0.5f;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 unitCircle = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
                topCircle[i] = center + offset + rotation * unitCircle;
                bottomCircle[i] = center - offset + rotation * unitCircle;
            }

            Handles.color = color;

            // Draw top circle
            for (int i = 0; i < segments; i++)
            {
                int nextIndex = (i + 1) % segments;
                Handles.DrawAAPolyLine(thickness, topCircle[i], topCircle[nextIndex]);
            }

            // Draw bottom circle
            for (int i = 0; i < segments; i++)
            {
                int nextIndex = (i + 1) % segments;
                Handles.DrawAAPolyLine(thickness, bottomCircle[i], bottomCircle[nextIndex]);
            }

            // Draw sides
            for (int i = 0; i < segments; i++)
            {
                Handles.DrawAAPolyLine(thickness, topCircle[i], bottomCircle[i]);
            }
        }

        public static void DrawAAPolyWireSphere(Vector3 center, float radius, int segments, float thickness, Color color)
        {
            Vector3[] horizontalCircle = new Vector3[segments];
            Vector3[] verticalCircle1 = new Vector3[segments];
            Vector3[] verticalCircle2 = new Vector3[segments];

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                horizontalCircle[i] = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                verticalCircle1[i] = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
                verticalCircle2[i] = center + new Vector3(0, Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            Handles.color = color;

            // Draw horizontal circle
            for (int i = 0; i < segments; i++)
            {
                int nextIndex = (i + 1) % segments;
                Handles.DrawAAPolyLine(thickness, horizontalCircle[i], horizontalCircle[nextIndex]);
            }

            // Draw first vertical circle
            for (int i = 0; i < segments; i++)
            {
                int nextIndex = (i + 1) % segments;
                Handles.DrawAAPolyLine(thickness, verticalCircle1[i], verticalCircle1[nextIndex]);
            }

            // Draw second vertical circle
            for (int i = 0; i < segments; i++)
            {
                int nextIndex = (i + 1) % segments;
                Handles.DrawAAPolyLine(thickness, verticalCircle2[i], verticalCircle2[nextIndex]);
            }
        }

        public static void DrawAAPolyArrow(Vector3 start, Vector3 end, float arrowHeadLength, float arrowHeadAngle, int arrowHeadSegments, float thickness, Color color)
        {
            Handles.color = color;

            // Draw the main line of the arrow
            Handles.DrawAAPolyLine(2f, start, end);

            // Calculate the direction of the arrow
            Vector3 direction = (end - start).normalized;

            // Calculate the base of the arrowhead
            float baseRadius = arrowHeadLength * Mathf.Tan(Mathf.Deg2Rad * arrowHeadAngle);
            Vector3 arrowHeadBaseCenter = end - direction * arrowHeadLength;

            // Calculate the vertices of the arrowhead base circle
            Vector3[] arrowHeadBaseVertices = new Vector3[arrowHeadSegments];
            for (int i = 0; i < arrowHeadSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / arrowHeadSegments;
                Vector3 baseVertex = new Vector3(Mathf.Cos(angle) * baseRadius, Mathf.Sin(angle) * baseRadius, 0);
                arrowHeadBaseVertices[i] = arrowHeadBaseCenter + Quaternion.LookRotation(direction) * baseVertex;
            }

            // Draw the arrowhead segments
            for (int i = 0; i < arrowHeadSegments; i++)
            {
                int nextIndex = (i + 1) % arrowHeadSegments;
                Handles.DrawAAPolyLine(2f, end, arrowHeadBaseVertices[i]); // Draw lines from the tip to the base vertices
                Handles.DrawAAPolyLine(2f, arrowHeadBaseVertices[i], arrowHeadBaseVertices[nextIndex]); // Draw lines connecting base vertices
            }
        }

        public static void DrawAAPolyCompass(Vector3 center, Quaternion rotation, float arrowLength, float labelSpace, float thickness, Color arrowColor, Color labelColor)
        {
            Handles.color = arrowColor;

            // Define compass directions
            Vector3 north = center + rotation * new Vector3(0, arrowLength, 0);
            Vector3 south = center + rotation * new Vector3(0, -arrowLength, 0);
            Vector3 east = center + rotation * new Vector3(arrowLength, 0, 0);
            Vector3 west = center + rotation * new Vector3(-arrowLength, 0, 0);

            // Draw arrows
            DrawArrow(center, north, arrowLength * 0.2f, 20f, 10, thickness, arrowColor);
            DrawArrow(center, south, arrowLength * 0.2f, 20f, 10, thickness, arrowColor);
            DrawArrow(center, east, arrowLength * 0.2f, 20f, 10, thickness, arrowColor);
            DrawArrow(center, west, arrowLength * 0.2f, 20f, 10, thickness, arrowColor);

            // Draw labels
            Handles.color = labelColor;
            GUIStyle style = new GUIStyle();
            style.normal.textColor = labelColor;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 20;
            style.alignment = TextAnchor.MiddleCenter;

            Handles.Label(north + rotation * new Vector3(0, labelSpace, 0), "N", style);
            Handles.Label(south + rotation * new Vector3(0, -labelSpace, 0), "S", style);
            Handles.Label(east + rotation * new Vector3(labelSpace, 0, 0), "E", style);
            Handles.Label(west + rotation * new Vector3(-labelSpace, 0, 0), "W", style);
        }

        public static void DrawAAPolyCompass(Vector3 center, Quaternion rotation, float arrowLength, float labelSpace, float thickness, Color baseArrowColor, Color northArrowColor, Color eastArrowColor, 
        Color southArrowColor, Color westArrowColor, Color labelColor)
        {
            Handles.color = baseArrowColor;

            // Define compass directions
            Vector3 north = center + rotation * new Vector3(0, arrowLength, 0);
            Vector3 south = center + rotation * new Vector3(0, -arrowLength, 0);
            Vector3 east = center + rotation * new Vector3(arrowLength, 0, 0);
            Vector3 west = center + rotation * new Vector3(-arrowLength, 0, 0);

            // Draw arrows
            DrawArrow(center, north, arrowLength * 0.2f, 20f, 10, thickness, northArrowColor);
            DrawArrow(center, south, arrowLength * 0.2f, 20f, 10, thickness, southArrowColor);
            DrawArrow(center, east, arrowLength * 0.2f, 20f, 10, thickness, eastArrowColor);
            DrawArrow(center, west, arrowLength * 0.2f, 20f, 10, thickness, westArrowColor);

            // Draw labels
            Handles.color = labelColor;
            GUIStyle style = new GUIStyle();
            style.normal.textColor = labelColor;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 20;
            style.alignment = TextAnchor.MiddleCenter;

            Handles.Label(north + rotation * new Vector3(0, labelSpace, 0), "N", style);
            Handles.Label(south + rotation * new Vector3(0, -labelSpace, 0), "S", style);
            Handles.Label(east + rotation * new Vector3(labelSpace, 0, 0), "E", style);
            Handles.Label(west + rotation * new Vector3(-labelSpace, 0, 0), "W", style);
        }

        private static void DrawArrow(Vector3 start, Vector3 end, float arrowHeadLength, float arrowHeadAngle, int arrowHeadSegments, float thickness, Color color)
        {
            Handles.color = color;

            // Draw the main line of the arrow
            Handles.DrawAAPolyLine(thickness, start, end);

            // Calculate the direction of the arrow
            Vector3 direction = (end - start).normalized;

            // Calculate the base of the arrowhead
            float baseRadius = arrowHeadLength * Mathf.Tan(Mathf.Deg2Rad * arrowHeadAngle);
            Vector3 arrowHeadBaseCenter = end - direction * arrowHeadLength;

            // Calculate the vertices of the arrowhead base circle
            Vector3[] arrowHeadBaseVertices = new Vector3[arrowHeadSegments];
            for (int i = 0; i < arrowHeadSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / arrowHeadSegments;
                Vector3 baseVertex = new Vector3(Mathf.Cos(angle) * baseRadius, Mathf.Sin(angle) * baseRadius, 0);
                arrowHeadBaseVertices[i] = arrowHeadBaseCenter + Quaternion.LookRotation(direction) * baseVertex;
            }

            // Draw the arrowhead segments
            for (int i = 0; i < arrowHeadSegments; i++)
            {
                int nextIndex = (i + 1) % arrowHeadSegments;
                Handles.DrawAAPolyLine(thickness, end, arrowHeadBaseVertices[i]); // Draw lines from the tip to the base vertices
                Handles.DrawAAPolyLine(thickness, arrowHeadBaseVertices[i], arrowHeadBaseVertices[nextIndex]); // Draw lines connecting base vertices
            }
        }

        public static void DrawAAPolyQuarterCircleWithArrow(Vector3 center, Quaternion rotation, float radius, int segments , bool isClockwise, float thickness, Color color, float labelSpace, Color labelColor)
        {
            Handles.color = color;
            Vector3[] arcPoints = new Vector3[segments + 1];

            // Calculate the points along the arc (quarter circle)
            for (int i = 0; i <= segments; i++)
            {
                float angle = isClockwise ? Mathf.Lerp(Mathf.PI / 2, 0, (float)i / segments) : Mathf.Lerp(0, Mathf.PI / 2, (float)i / segments); // 90 to 0 degrees (PI/2 to 0 radians) or 0 to 90 degrees (0 to PI/2 radians)
                Vector3 point = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
                arcPoints[i] = center + rotation * point;
            }

            // Draw the arc
            for (int i = 0; i < segments; i++)
            {
                Handles.DrawAAPolyLine(thickness, arcPoints[i], arcPoints[i + 1]);
            }

            // Draw the arrowhead at the end of the arc
            Vector3 endPoint = arcPoints[segments];
            Vector3 direction = (arcPoints[segments] - arcPoints[segments - 1]).normalized; // Direction of the arrowhead
            DrawArrowHead(endPoint, direction, radius * 0.2f, 20f, 10, thickness, color);

            // Draw the label "Clockwise" or "Counterclockwise"
            Handles.color = labelColor;
            GUIStyle style = new GUIStyle
            {
                normal = { textColor = labelColor },
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };
            
            Vector3 labelPosition = center + rotation * new Vector3(radius * Mathf.Cos(Mathf.Deg2Rad * 45), radius * Mathf.Sin(Mathf.Deg2Rad * 45), 0);
            Handles.Label(labelPosition + new Vector3(0.1f, 0.1f, 0), isClockwise ? "CW Rotation" : "CCW Rotation", style);
        }

        private static void DrawArrowHead(Vector3 tip, Vector3 direction, float arrowHeadLength, float arrowHeadAngle, int arrowHeadSegments, float thickness, Color color)
        {
            Handles.color = color;

            // Calculate the base of the arrowhead
            float baseRadius = arrowHeadLength * Mathf.Tan(Mathf.Deg2Rad * arrowHeadAngle);
            Vector3 arrowHeadBaseCenter = tip - direction * arrowHeadLength;

            // Calculate the vertices of the arrowhead base circle
            Vector3[] arrowHeadBaseVertices = new Vector3[arrowHeadSegments];
            for (int i = 0; i < arrowHeadSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / arrowHeadSegments;
                Vector3 baseVertex = new Vector3(Mathf.Cos(angle) * baseRadius, Mathf.Sin(angle) * baseRadius, 0);
                arrowHeadBaseVertices[i] = arrowHeadBaseCenter + Quaternion.LookRotation(direction) * baseVertex;
            }

            // Draw the arrowhead segments
            for (int i = 0; i < arrowHeadSegments; i++)
            {
                int nextIndex = (i + 1) % arrowHeadSegments;
                Handles.DrawAAPolyLine(thickness, tip, arrowHeadBaseVertices[i]); // Draw lines from the tip to the base vertices
                Handles.DrawAAPolyLine(thickness, arrowHeadBaseVertices[i], arrowHeadBaseVertices[nextIndex]); // Draw lines connecting base vertices
            }
        }
    }
}
#endif
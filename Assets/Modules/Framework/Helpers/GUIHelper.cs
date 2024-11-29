using UnityEngine;

namespace Framework
{
    public class GUIHelper : MonoBehaviour
    {
        public static void DrawRectangleEdges(Rect rect, Color color, float thickness)
        {
            // Top edge
            DrawLine(new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), thickness, color);
            // Bottom edge
            DrawLine(new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMax, rect.yMax), thickness, color);
            // Left edge
            DrawLine(new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax), thickness, color);
            // Right edge
            DrawLine(new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), thickness, color);
        }

        public static void DrawFilledRect(Rect rect, Color color)
        {
            // Save previous GUI color
            Color prevColor = GUI.color;
            GUI.color = color;

            // Draw a filled rectangle
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            // Restore previous GUI color
            GUI.color = prevColor;
        }

        public static void DrawLine(Vector2 start, Vector2 end, float thickness, Color color)
        {
            Color prevColor = GUI.color;
            GUI.color = color;
            float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(start, end);

            Matrix4x4 matrixBackup = GUI.matrix;

            GUIUtility.RotateAroundPivot(angle, start);
            GUI.DrawTexture(new Rect(start.x, start.y, length, thickness), Texture2D.whiteTexture);

            GUI.matrix = matrixBackup;
            GUI.color = prevColor;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Easy.BehaviourTree;
using UnityEditor;
using UnityEngine;

namespace Easy.BehaviourTree.Editor
{
    public sealed class BehaviourTreeDebuggerWindow : EditorWindow
    {
        private const float NodeWidth = 190f;
        private const float NodeHeight = 88f;
        private const float HorizontalSpacing = 36f;
        private const float VerticalSpacing = 96f;

        private UnityEngine.Object targetObject;
        private int selectedFieldIndex;
        private Vector2 scroll;
        private List<TreeField> fields = new List<TreeField>();
        private List<GraphNode> graphNodes = new List<GraphNode>();
        private object rootNode;
        private double nextRefresh;

        [MenuItem("Window/Easy Toolkit/Behaviour Tree Debugger")]
        public static void Open()
        {
            GetWindow<BehaviourTreeDebuggerWindow>("Behaviour Tree Debugger");
        }

        private void OnEnable()
        {
            Selection.selectionChanged += Repaint;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
        }

        private void OnGUI()
        {
            DrawToolbar();

            var refreshInterval = EditorApplication.isPlaying ? 0.05f : 0.2f;
            if (EditorApplication.timeSinceStartup > nextRefresh)
            {
                nextRefresh = EditorApplication.timeSinceStartup + refreshInterval;
                RefreshFieldValues();
                Repaint();
            }

            if (rootNode == null)
            {
                EditorGUILayout.HelpBox("Select a component or object with a BehaviourTree INode field, then choose the field to visualize.", MessageType.Info);
                return;
            }

            DrawGraph();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var nextTarget = EditorGUILayout.ObjectField(targetObject, typeof(UnityEngine.Object), true, GUILayout.MinWidth(240f));
                if (nextTarget != targetObject)
                {
                    targetObject = nextTarget;
                    RefreshFields();
                }

                if (GUILayout.Button("Use Selection", EditorStyles.toolbarButton, GUILayout.Width(96f)))
                {
                    targetObject = Selection.activeObject;
                    RefreshFields();
                }

                using (new EditorGUI.DisabledScope(fields.Count == 0))
                {
                    var names = GetFieldNames();
                    var nextIndex = EditorGUILayout.Popup(selectedFieldIndex, names, EditorStyles.toolbarPopup, GUILayout.Width(260f));
                    if (nextIndex != selectedFieldIndex)
                    {
                        selectedFieldIndex = nextIndex;
                        RefreshGraph();
                    }
                }

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                    RefreshFields();

                using (new EditorGUI.DisabledScope(fields.Count == 0))
                {
                    if (GUILayout.Button("Instrument Field", EditorStyles.toolbarButton, GUILayout.Width(112f)))
                        InstrumentSelectedField();
                }

                if (GUILayout.Button("Clear State", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                    BehaviourTreeDebugRegistry.Clear();

                using (new EditorGUI.DisabledScope(rootNode == null))
                {
                    if (GUILayout.Button("Copy Snapshot", EditorStyles.toolbarButton, GUILayout.Width(104f)))
                        CopySnapshot();

                    if (GUILayout.Button("Save Snapshot...", EditorStyles.toolbarButton, GUILayout.Width(112f)))
                        SaveSnapshot();
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void CopySnapshot()
        {
            string snapshot = CaptureSnapshot();
            EditorGUIUtility.systemCopyBuffer = snapshot;
            ShowNotification(new GUIContent("Behaviour tree snapshot copied."));
        }

        private void SaveSnapshot()
        {
            string snapshot = CaptureSnapshot();
            string projectDirectory = Directory.GetParent(Application.dataPath)?.FullName
                ?? Application.dataPath;
            string path = EditorUtility.SaveFilePanel(
                "Save Behaviour Tree Snapshot",
                projectDirectory,
                BuildSnapshotFileName(),
                "txt");
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                File.WriteAllText(path, snapshot, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                ShowNotification(new GUIContent("Behaviour tree snapshot saved."));
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog(
                    "Snapshot Save Failed",
                    exception.GetType().Name + ": " + exception.Message,
                    "OK");
            }
        }

        private string CaptureSnapshot()
        {
            RefreshFieldValues();

            var builder = new StringBuilder(4096);
            builder.AppendLine("EasyToolkit Behaviour Tree Debugger Snapshot");
            builder.Append("Captured UTC: ").AppendLine(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            builder.Append("Unity: ").AppendLine(Application.unityVersion);
            builder.Append("Editor state: ")
                .Append(EditorApplication.isPlaying ? "Playing" : "Editing")
                .Append(EditorApplication.isPaused ? ", Paused" : string.Empty)
                .AppendLine();
            if (EditorApplication.isPlaying)
                builder.Append("Frame: ").Append(Time.frameCount).AppendLine();

            AppendTargetSnapshot(builder);
            AppendComponentSnapshot(builder);
            AppendTreeSnapshot(builder);
            AppendBlackboardSnapshot(builder);
            return builder.ToString();
        }

        private void AppendTargetSnapshot(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("Target");
            builder.Append("  Object: ").AppendLine(FormatObject(targetObject));
            if (fields.Count == 0)
            {
                builder.AppendLine("  Tree member: <none>");
                return;
            }

            TreeField field = fields[Mathf.Clamp(selectedFieldIndex, 0, fields.Count - 1)];
            builder.Append("  Owner: ").AppendLine(FormatObject(field.Owner));
            builder.Append("  Tree member: ")
                .Append(field.Owner.GetType().FullName)
                .Append('.')
                .AppendLine(field.Name);
            builder.Append("  Root type: ")
                .AppendLine(rootNode != null ? UnwrapObservedProxy(rootNode).GetType().FullName : "<null>");
        }

        private void AppendComponentSnapshot(StringBuilder builder)
        {
            GameObject gameObject = targetObject as GameObject;
            if (gameObject == null && targetObject is Component component)
                gameObject = component.gameObject;
            if (gameObject == null)
                return;

            builder.AppendLine();
            builder.AppendLine("Component Diagnostics");
            Component[] components = gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component current = components[i];
                if (current == null || !IsDiagnosticType(current.GetType()))
                    continue;

                Type type = current.GetType();
                builder.Append("  ").AppendLine(type.FullName);
                PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
                for (int propertyIndex = 0; propertyIndex < properties.Length; propertyIndex++)
                {
                    PropertyInfo property = properties[propertyIndex];
                    if (!IsDiagnosticProperty(property))
                        continue;

                    object value;
                    try
                    {
                        value = property.GetValue(current);
                    }
                    catch (Exception exception)
                    {
                        builder.Append("    ").Append(property.Name)
                            .Append(" = <").Append(exception.GetType().Name).AppendLine(">");
                        continue;
                    }

                    if (value is IDictionary dictionary)
                    {
                        AppendDictionary(builder, "  " + property.Name, dictionary);
                        continue;
                    }

                    if (value is IEnumerable && value is not string)
                        continue;

                    builder.Append("    ").Append(property.Name).Append(" = ")
                        .AppendLine(FormatValue(value));
                }
            }
        }

        private static bool IsDiagnosticType(Type type)
        {
            string typeNamespace = type?.Namespace;
            // Diagnose first-party and behaviour-tree types; skip engine and BCL types.
            return !string.IsNullOrEmpty(typeNamespace)
                && !typeNamespace.StartsWith("System", StringComparison.Ordinal)
                && !typeNamespace.StartsWith("Unity", StringComparison.Ordinal)
                && !typeNamespace.StartsWith("TMPro", StringComparison.Ordinal);
        }

        private static bool IsDiagnosticProperty(PropertyInfo property)
        {
            if (property == null
                || property.GetMethod == null
                || property.GetMethod.IsStatic
                || property.GetIndexParameters().Length != 0)
            {
                return false;
            }

            return IsDiagnosticType(property.DeclaringType);
        }

        private void AppendTreeSnapshot(StringBuilder builder)
        {
            builder.AppendLine();
            builder.Append("Nodes (").Append(graphNodes.Count).AppendLine(")");
            if (graphNodes.Count == 0)
            {
                builder.AppendLine("  <none>");
                return;
            }

            for (int i = 0; i < graphNodes.Count; i++)
            {
                GraphNode node = graphNodes[i];
                BehaviourTreeDebugInfo info = BehaviourTreeDebugRegistry.Get(node.Source);
                builder.Append(' ', node.Depth * 2)
                    .Append("- [").Append(i).Append("] ")
                    .Append(node.Title)
                    .Append(" | ").Append(node.Kind)
                    .Append(" | ").Append(node.Source != null ? node.Source.GetType().FullName : "<null>")
                    .Append(" | ").AppendLine(FormatState(info));

                if (!string.IsNullOrEmpty(node.Description))
                {
                    builder.Append(' ', node.Depth * 2 + 4)
                        .Append("Description: ")
                        .AppendLine(node.Description);
                }

                if (info != null && !string.IsNullOrEmpty(info.LastException))
                {
                    builder.Append(' ', node.Depth * 2 + 4)
                        .Append("Exception: ")
                        .AppendLine(info.LastException);
                }
            }
        }

        private void AppendBlackboardSnapshot(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("Blackboard");
            object blackboard = FindBlackboard(rootNode);
            if (blackboard == null)
            {
                builder.AppendLine("  <not available>");
                return;
            }

            Type type = blackboard.GetType();
            builder.Append("  Type: ").AppendLine(type.FullName);
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (property.GetMethod == null || property.GetIndexParameters().Length != 0)
                    continue;

                object value;
                try
                {
                    value = property.GetValue(blackboard);
                }
                catch (Exception exception)
                {
                    builder.Append("  ").Append(property.Name)
                        .Append(" = <").Append(exception.GetType().Name).AppendLine(">");
                    continue;
                }

                if (value is IDictionary dictionary)
                {
                    AppendDictionary(builder, property.Name, dictionary);
                    continue;
                }

                builder.Append("  ").Append(property.Name).Append(" = ")
                    .AppendLine(FormatValue(value));
            }
        }

        private static void AppendDictionary(StringBuilder builder, string name, IDictionary dictionary)
        {
            builder.Append("  ").Append(name).Append(" (").Append(dictionary.Count).AppendLine(")");
            foreach (DictionaryEntry entry in dictionary)
            {
                builder.Append("    ").Append(FormatValue(entry.Key)).Append(" = ")
                    .AppendLine(FormatValue(entry.Value));
            }
        }

        private static object FindBlackboard(object node)
        {
            node = UnwrapObservedProxy(node);
            if (node == null)
                return null;

            Type type = node.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    "blackboard",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field.GetValue(node);

                type = type.BaseType;
            }

            return null;
        }

        private string BuildSnapshotFileName()
        {
            string targetName = targetObject != null ? targetObject.name : "BehaviourTree";
            foreach (char invalid in Path.GetInvalidFileNameChars())
                targetName = targetName.Replace(invalid, '_');

            return targetName + "_BehaviourTree_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
        }

        private static string FormatObject(object value)
        {
            if (value is UnityEngine.Object destroyedObject && destroyedObject == null)
                return "<destroyed " + value.GetType().FullName + ">";

            if (value is Component component)
                return GetHierarchyPath(component.transform) + " (" + component.GetType().FullName + ", " + ObjectId(component) + ")";

            if (value is GameObject gameObject)
                return GetHierarchyPath(gameObject.transform) + " (GameObject, " + ObjectId(gameObject) + ")";

            if (value is UnityEngine.Object unityObject)
                return unityObject.name + " (" + unityObject.GetType().FullName + ", " + ObjectId(unityObject) + ")";

            return value != null ? value.GetType().FullName : "<null>";
        }

        private static string ObjectId(UnityEngine.Object value)
        {
#if UNITY_6000_4_OR_NEWER
            return value.GetEntityId().ToString();
#else
            return value.GetInstanceID().ToString(CultureInfo.InvariantCulture);
#endif
        }

        private static string FormatValue(object value)
        {
            if (value == null)
                return "<null>";

            if (value is string text)
                return "\"" + text.Replace("\r", "\\r").Replace("\n", "\\n") + "\"";

            if (value is UnityEngine.Object)
                return FormatObject(value);

            if (value is IFormattable formattable)
                return formattable.ToString(null, CultureInfo.InvariantCulture);

            Type type = value.GetType();
            if (type.IsValueType && IsDiagnosticType(type))
                return FormatStructuredValue(value, type);

            return value.ToString();
        }

        private static string FormatStructuredValue(object value, Type type)
        {
            var builder = new StringBuilder();
            builder.Append(type.Name).Append(" { ");
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            int written = 0;
            for (int i = 0; i < properties.Length && written < 12; i++)
            {
                PropertyInfo property = properties[i];
                if (property.GetMethod == null || property.GetIndexParameters().Length != 0)
                    continue;

                object propertyValue;
                try
                {
                    propertyValue = property.GetValue(value);
                }
                catch (Exception)
                {
                    continue;
                }

                if (written > 0)
                    builder.Append(", ");
                builder.Append(property.Name).Append('=').Append(FormatValue(propertyValue));
                written++;
            }

            return builder.Append(" }").ToString();
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "<destroyed>";

            var builder = new StringBuilder(transform.name);
            Transform parent = transform.parent;
            while (parent != null)
            {
                builder.Insert(0, '/');
                builder.Insert(0, parent.name);
                parent = parent.parent;
            }

            return builder.ToString();
        }

        private void DrawGraph()
        {
            var graphRect = new Rect(0, EditorGUIUtility.singleLineHeight + 6f, position.width, position.height - EditorGUIUtility.singleLineHeight - 6f);
            var contentRect = CalculateContentRect();
            scroll = GUI.BeginScrollView(graphRect, scroll, contentRect);

            Handles.BeginGUI();
            foreach (var node in graphNodes)
            {
                foreach (var child in node.Children)
                    DrawConnection(node.Rect, child.Rect);
            }
            Handles.EndGUI();

            foreach (var node in graphNodes)
                DrawNode(node);

            GUI.EndScrollView();
        }

        private void DrawNode(GraphNode node)
        {
            var info = BehaviourTreeDebugRegistry.Get(node.Source);
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = GetNodeColor(info);
            GUI.Box(node.Rect, GUIContent.none, EditorStyles.helpBox);
            GUI.backgroundColor = previousColor;

            var labelRect = new Rect(node.Rect.x + 8f, node.Rect.y + 7f, node.Rect.width - 16f, 18f);
            EditorGUI.LabelField(labelRect, node.Title, EditorStyles.boldLabel);

            var typeRect = new Rect(node.Rect.x + 8f, node.Rect.y + 27f, node.Rect.width - 16f, 18f);
            EditorGUI.LabelField(typeRect, node.Kind);

            var descriptionRect = new Rect(node.Rect.x + 8f, node.Rect.y + 46f, node.Rect.width - 16f, 18f);
            EditorGUI.LabelField(descriptionRect, node.Description, EditorStyles.miniLabel);

            var stateRect = new Rect(node.Rect.x + 8f, node.Rect.y + 65f, node.Rect.width - 16f, 18f);
            EditorGUI.LabelField(stateRect, FormatState(info));
        }

        private static void DrawConnection(Rect parent, Rect child)
        {
            var start = new Vector3(parent.center.x, parent.yMax, 0f);
            var end = new Vector3(child.center.x, child.yMin, 0f);
            var tangent = Vector3.up * 36f;
            Handles.DrawBezier(start, end, start + tangent, end - tangent, new Color(0.45f, 0.45f, 0.45f), null, 2f);
        }

        private void RefreshFields()
        {
            fields.Clear();
            selectedFieldIndex = 0;
            rootNode = null;

            if (targetObject == null)
                targetObject = Selection.activeObject;

            if (targetObject is GameObject gameObject)
            {
                foreach (var component in gameObject.GetComponents<Component>())
                    AddFields(component);
            }
            else
            {
                AddFields(targetObject);
            }

            RefreshGraph();
        }

        private void InstrumentSelectedField()
        {
            if (fields.Count == 0)
                return;

            var field = fields[Mathf.Clamp(selectedFieldIndex, 0, fields.Count - 1)];
            var wrapped = WrapNode(field.Value);
            if (wrapped == null)
            {
                ShowNotification(new GUIContent("Selected field is not an INode tree."));
                return;
            }

            if (!field.CanWrite)
            {
                ShowNotification(new GUIContent("Selected tree member is read-only and cannot be instrumented."));
                return;
            }

            if (!field.MemberType.IsAssignableFrom(wrapped.GetType()))
            {
                ShowNotification(new GUIContent("Field type must be assignable from INode<T,V> to store an instrumented wrapper."));
                return;
            }

            try
            {
                field.SetValue(wrapped);
                field.Value = wrapped;
                rootNode = wrapped;
                RefreshGraph();
            }
            catch (Exception exception)
            {
                ShowNotification(new GUIContent("Could not write instrumented tree: " + exception.GetType().Name));
            }
        }

        private void AddFields(object owner)
        {
            if (owner == null)
                return;

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var type = owner.GetType();
            while (type != null)
            {
                foreach (var field in type.GetFields(flags))
                {
                    if (!IsNodeType(field.FieldType))
                        continue;

                    var value = field.GetValue(owner);
                    fields.Add(new TreeField(owner, field, value));
                }

                foreach (var property in type.GetProperties(flags))
                {
                    if (!IsNodeType(property.PropertyType)
                        || property.GetIndexParameters().Length != 0
                        || property.GetMethod == null)
                    {
                        continue;
                    }

                    try
                    {
                        var value = property.GetValue(owner);
                        fields.Add(new TreeField(owner, property, value));
                    }
                    catch (Exception)
                    {
                    }
                }

                type = type.BaseType;
            }
        }

        private void RefreshGraph()
        {
            rootNode = fields.Count == 0 ? null : fields[Mathf.Clamp(selectedFieldIndex, 0, fields.Count - 1)].Value;
            graphNodes.Clear();

            if (rootNode != null)
                BuildGraph(rootNode);
        }

        private void RefreshFieldValues()
        {
            if (targetObject == null || fields.Count == 0)
                return;

            bool graphChanged = false;
            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (!field.IsOwnerAlive)
                {
                    targetObject = null;
                    RefreshFields();
                    return;
                }

                object value;
                try
                {
                    value = field.GetValue();
                }
                catch (Exception)
                {
                    targetObject = null;
                    RefreshFields();
                    return;
                }

                if (ReferenceEquals(value, field.Value))
                    continue;

                field.Value = value;
                if (i == selectedFieldIndex)
                    graphChanged = true;
            }

            if (graphChanged)
                RefreshGraph();
        }

        private void BuildGraph(object root)
        {
            var visited = new HashSet<object>();
            var rootGraph = BuildNode(UnwrapObservedProxy(root), 0, visited);
            var nextX = 18f;
            Layout(rootGraph, ref nextX);
        }

        private GraphNode BuildNode(object source, int depth, HashSet<object> visited)
        {
            source = UnwrapObservedProxy(source);
            var node = new GraphNode(source, GetNodeTitle(source), GetNodeKind(source), GetNodeDescription(source), depth);
            graphNodes.Add(node);

            if (source == null || !visited.Add(source))
                return node;

            foreach (var childSource in GetChildren(source))
                node.Children.Add(BuildNode(childSource, depth + 1, visited));

            return node;
        }

        private float Layout(GraphNode node, ref float nextX)
        {
            if (node.Children.Count == 0)
            {
                node.Rect = new Rect(nextX, 18f + node.Depth * VerticalSpacing, NodeWidth, NodeHeight);
                nextX += NodeWidth + HorizontalSpacing;
                return node.Rect.center.x;
            }

            var first = 0f;
            var last = 0f;
            for (int i = 0; i < node.Children.Count; i++)
            {
                var center = Layout(node.Children[i], ref nextX);
                if (i == 0)
                    first = center;
                last = center;
            }

            var x = (first + last) * 0.5f - NodeWidth * 0.5f;
            node.Rect = new Rect(x, 18f + node.Depth * VerticalSpacing, NodeWidth, NodeHeight);
            return node.Rect.center.x;
        }

        private Rect CalculateContentRect()
        {
            var rect = new Rect(0, 0, position.width, position.height);
            foreach (var node in graphNodes)
            {
                rect.xMax = Mathf.Max(rect.xMax, node.Rect.xMax + 24f);
                rect.yMax = Mathf.Max(rect.yMax, node.Rect.yMax + 24f);
            }

            return rect;
        }

        private string[] GetFieldNames()
        {
            if (fields.Count == 0)
                return new[] { "No tree fields found" };

            var names = new string[fields.Count];
            for (int i = 0; i < fields.Count; i++)
            {
                names[i] = fields[i].Owner.GetType().Name + "." + fields[i].Name;
            }

            return names;
        }

        private static IEnumerable<object> GetChildren(object node)
        {
            var type = node.GetType();
            var childProperty = FindProperty(type, "Child");
            if (childProperty != null)
            {
                var child = UnwrapObservedProxy(childProperty.GetValue(node));
                if (child != null)
                    yield return child;
            }

            var childrenProperty = FindProperty(type, "Childs");
            if (childrenProperty != null && childrenProperty.GetValue(node) is IEnumerable children)
            {
                foreach (var child in children)
                {
                    var unwrappedChild = UnwrapObservedProxy(child);
                    if (unwrappedChild != null)
                        yield return unwrappedChild;
                }
            }
        }

        private static object UnwrapObservedProxy(object node)
        {
            while (node != null && InheritsGeneric(node.GetType(), typeof(ObservedProxyDecorator<,>)))
            {
                var childProperty = FindProperty(node.GetType(), "Child");
                if (childProperty == null)
                    return node;

                var child = childProperty.GetValue(node);
                if (child == null || ReferenceEquals(child, node))
                    return node;

                node = child;
            }

            return node;
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            while (type != null)
            {
                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                    return property;

                type = type.BaseType;
            }

            return null;
        }

        private static bool IsNodeType(Type type)
        {
            if (type == null)
                return false;

            if (type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(INode<,>))
                return true;

            foreach (var interfaceType in type.GetInterfaces())
            {
                if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(INode<,>))
                    return true;
            }

            return false;
        }

        private static object WrapNode(object node)
        {
            if (node == null)
                return null;

            var nodeInterface = FindNodeInterface(node.GetType());
            if (nodeInterface == null)
                return null;

            var args = nodeInterface.GetGenericArguments();
            var method = typeof(BehaviourTreeDebugInstrumenter).GetMethod("Wrap", BindingFlags.Public | BindingFlags.Static);
            var genericMethod = method.MakeGenericMethod(args);
            return genericMethod.Invoke(null, new[] { node });
        }

        private static Type FindNodeInterface(Type type)
        {
            if (type == null)
                return null;

            if (type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(INode<,>))
                return type;

            foreach (var interfaceType in type.GetInterfaces())
            {
                if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(INode<,>))
                    return interfaceType;
            }

            return null;
        }

        private static string GetNodeTitle(object node)
        {
            if (node == null)
                return "Null";

            if (node is IBehaviourTreeDebugNote note && !string.IsNullOrEmpty(note.Name))
                return note.Name;

            var type = node.GetType();
            if (type.IsGenericType)
                return type.Name.Substring(0, type.Name.IndexOf('`'));

            return type.Name;
        }

        private static string GetNodeDescription(object node)
        {
            if (node == null)
                return string.Empty;

            if (node is IBehaviourTreeDebugNote note)
            {
                if (!string.IsNullOrEmpty(note.Description))
                    return FormatNoteDescription(note.Key, note.Description);

                var childProperty = FindProperty(node.GetType(), "Child");
                var child = childProperty != null ? UnwrapObservedProxy(childProperty.GetValue(node)) : null;
                if (child != null)
                    return FormatNoteDescription(note.Key, GetFriendlyTypeName(child.GetType()));

                return FormatNoteDescription(note.Key, string.Empty);
            }

            return GetFriendlyTypeName(node.GetType());
        }

        private static string FormatNoteDescription(string key, string description)
        {
            if (string.IsNullOrEmpty(key))
                return description;

            if (string.IsNullOrEmpty(description))
                return "[" + key + "]";

            return "[" + key + "] " + description;
        }

        private static string GetNodeKind(object node)
        {
            if (node == null)
                return "Missing";

            var type = node.GetType();
            if (InheritsGeneric(type, typeof(CompositeNode<,>)))
                return "Composite";

            if (InheritsGeneric(type, typeof(DecoratorNode<,>)))
            {
                if (node is IBehaviourTreeDebugNote)
                    return "Note";

                return "Decorator";
            }

            return "Leaf";
        }

        private static string GetFriendlyTypeName(Type type)
        {
            if (type == null)
                return string.Empty;

            if (!type.IsGenericType)
                return type.Name;

            var name = type.Name.Substring(0, type.Name.IndexOf('`'));
            var args = type.GetGenericArguments();
            var argNames = new string[args.Length];
            for (int i = 0; i < args.Length; i++)
                argNames[i] = args[i].Name;

            return name + "<" + string.Join(", ", argNames) + ">";
        }

        private static bool InheritsGeneric(Type type, Type genericType)
        {
            while (type != null)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == genericType)
                    return true;

                type = type.BaseType;
            }

            return false;
        }

        private static string FormatState(BehaviourTreeDebugInfo info)
        {
            if (info == null)
                return "Not observed";

            if (info.IsExecuting)
                return "EXECUTING  #" + info.RunCount;

            if (!string.IsNullOrEmpty(info.LastException))
                return "Exception";

            var result = info.LastResult.HasValue ? info.LastResult.Value.ToString() : "Pending";
            var current = BehaviourTreeDebugRegistry.WasVisitedInLatestRun(info) ? "Current" : "Stale";
            return current + "  " + result + "  #" + info.RunCount + "  " + info.LastDurationMs.ToString("0.##") + " ms";
        }

        private static Color GetNodeColor(BehaviourTreeDebugInfo info)
        {
            if (info == null)
                return new Color(0.8f, 0.8f, 0.8f);

            if (info.IsExecuting)
                return new Color(1f, 0.92f, 0.35f);

            if (!string.IsNullOrEmpty(info.LastException))
                return new Color(1f, 0.35f, 0.35f);

            if (!info.LastResult.HasValue)
                return new Color(0.8f, 0.8f, 0.8f);

            if (!BehaviourTreeDebugRegistry.WasVisitedInLatestRun(info))
                return new Color(0.44f, 0.44f, 0.44f);

            switch (info.LastResult.Value)
            {
                case Result.SUCCESS:
                    return new Color(0.38f, 0.82f, 0.45f);
                case Result.RUNNING:
                    return new Color(0.43f, 0.64f, 1f);
                case Result.FAILED:
                    return new Color(1f, 0.67f, 0.32f);
                default:
                    return new Color(0.8f, 0.8f, 0.8f);
            }
        }

        private sealed class TreeField
        {
            public TreeField(object owner, FieldInfo field, object value)
            {
                Owner = owner;
                Field = field;
                Value = value;
            }

            public TreeField(object owner, PropertyInfo property, object value)
            {
                Owner = owner;
                Property = property;
                Value = value;
            }

            public object Owner { get; }
            public FieldInfo Field { get; }
            public PropertyInfo Property { get; }
            public string Name => Field != null ? Field.Name : Property.Name;
            public Type MemberType => Field != null ? Field.FieldType : Property.PropertyType;
            public bool CanWrite => Field != null || Property.SetMethod != null;
            public bool IsOwnerAlive => Owner is not UnityEngine.Object unityOwner || unityOwner != null;
            public object Value { get; set; }

            public object GetValue()
            {
                return Field != null ? Field.GetValue(Owner) : Property.GetValue(Owner);
            }

            public void SetValue(object value)
            {
                if (Field != null)
                {
                    Field.SetValue(Owner, value);
                    return;
                }

                Property.SetValue(Owner, value);
            }
        }

        private sealed class GraphNode
        {
            public GraphNode(object source, string title, string kind, string description, int depth)
            {
                Source = source;
                Title = title;
                Kind = kind;
                Description = description;
                Depth = depth;
            }

            public object Source { get; }
            public string Title { get; }
            public string Kind { get; }
            public string Description { get; }
            public int Depth { get; }
            public Rect Rect { get; set; }
            public List<GraphNode> Children { get; } = new List<GraphNode>();
        }
    }
}

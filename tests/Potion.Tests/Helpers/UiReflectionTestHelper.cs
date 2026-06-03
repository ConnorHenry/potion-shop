using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

internal static class UiReflectionTestHelper
{
    private static readonly Lazy<Assembly> UiAssembly = new(LoadUiAssembly);
    private static bool _resolverRegistered;

    public static Type GetTypeFromUiAssembly(string typeName)
    {
        var assembly = UiAssembly.Value;
        var type = assembly.GetType(typeName)
            ?? assembly.GetTypes().FirstOrDefault(x => string.Equals(x.Name, typeName, StringComparison.Ordinal));

        if (type is null)
            throw new InvalidOperationException($"Type '{typeName}' not found in OccultShop assembly.");

        return type;
    }

    public static T InvokePrivateStatic<T>(string typeName, string methodName, params object?[] args)
    {
        var result = InvokePrivateStatic(typeName, methodName, args);
        if (result is T typed)
            return typed;

        throw new InvalidOperationException($"Method {typeName}.{methodName} did not return {typeof(T).Name}.");
    }

    public static object? InvokePrivateStatic(string typeName, string methodName, params object?[] args)
    {
        var type = GetTypeFromUiAssembly(typeName);
        var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Missing method {typeName}.{methodName}.");

        return method.Invoke(null, args);
    }

    public static object? InvokeInstance(object target, string methodName, params object?[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing method {target.GetType().Name}.{methodName}.");

        return method.Invoke(target, args);
    }

    public static void SetProperty(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"Property '{propertyName}' not found on {target.GetType().Name}.");
        property.SetValue(target, value);
    }

    public static T GetProperty<T>(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"Property '{propertyName}' not found on {target.GetType().Name}.");
        var value = property.GetValue(target);

        if (value is T typed)
            return typed;

        throw new InvalidOperationException($"Property '{propertyName}' on {target.GetType().Name} is not {typeof(T).Name}.");
    }

    private static Assembly LoadUiAssembly()
    {
        RegisterAssemblyResolver();

        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var assemblyPath = Path.Combine(projectRoot, ".godot", "mono", "temp", "bin", "Debug", "OccultShop.dll");

        if (!File.Exists(assemblyPath))
            throw new InvalidOperationException($"OccultShop assembly not found at '{assemblyPath}'. Build OccultShop first.");

        return Assembly.LoadFrom(assemblyPath);
    }

    private static void RegisterAssemblyResolver()
    {
        if (_resolverRegistered)
            return;

        AssemblyLoadContext.Default.Resolving += ResolveFromNuGetPackages;
        _resolverRegistered = true;
    }

    private static Assembly? ResolveFromNuGetPackages(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName.Name))
            return null;

        var assemblyFileName = $"{assemblyName.Name}.dll";
        foreach (var packageRoot in GetNuGetPackageRoots())
        {
            var packageDirectory = Path.Combine(packageRoot, assemblyName.Name.ToLowerInvariant());
            if (!Directory.Exists(packageDirectory))
                continue;

            foreach (var versionDirectory in Directory.GetDirectories(packageDirectory).OrderByDescending(x => x, StringComparer.Ordinal))
            {
                var candidatePath = Path.Combine(versionDirectory, "lib", "net8.0", assemblyFileName);
                if (File.Exists(candidatePath))
                    return context.LoadFromAssemblyPath(candidatePath);
            }
        }

        return null;
    }

    private static IEnumerable<string> GetNuGetPackageRoots()
    {
        var roots = new List<string>();
        var configuredRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrWhiteSpace(configuredRoot))
            roots.Add(configuredRoot);

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
            roots.Add(Path.Combine(userProfile, ".nuget", "packages"));

        return roots
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}

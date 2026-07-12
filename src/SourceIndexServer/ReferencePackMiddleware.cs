using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.SourceBrowser.SourceIndexServer.Models;

namespace Microsoft.SourceBrowser.SourceIndexServer
{
    // Serves per-symbol reference files out of the packed references.pack/references.index produced by the
    // HtmlGenerator. Matches requests of the form /{assembly}/R/{16hex}.html and returns the exact packed
    // bytes; anything that doesn't match, or whose assembly has no pack, falls through to the static file
    // middleware so the MSBuild/Guid assemblies (which still emit individual files) keep working.
    public sealed class ReferencePackMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _rootPath;
        private readonly ConcurrentDictionary<string, Lazy<ReferencePack>> _packs = new(StringComparer.OrdinalIgnoreCase);

        public ReferencePackMiddleware(RequestDelegate next, string rootPath)
        {
            _next = next;
            _rootPath = rootPath;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (TryMatch(context.Request.Path.Value, out string assembly, out string symbolId) &&
                TryGetPack(assembly) is ReferencePack pack &&
                pack.TryGetFragment(symbolId, out byte[] fragment))
            {
                context.Response.ContentType = "text/html";
                context.Response.ContentLength = fragment.Length;
                await context.Response.Body.WriteAsync(fragment, 0, fragment.Length);
                return;
            }

            await _next(context);
        }

        // Matches /{assembly}/R/{id}.html where id is exactly 16 lowercase hex characters. The assembly
        // segment is taken verbatim; a nested id path (e.g. an extra '/') would fail the segment count.
        private static bool TryMatch(string path, out string assembly, out string symbolId)
        {
            assembly = null;
            symbolId = null;

            if (string.IsNullOrEmpty(path) || path[0] != '/')
            {
                return false;
            }

            int rSegment = path.IndexOf("/R/", StringComparison.Ordinal);
            if (rSegment <= 0)
            {
                return false;
            }

            assembly = path.Substring(1, rSegment - 1);
            if (assembly.Length == 0 || assembly.IndexOf('/') >= 0)
            {
                return false;
            }

            var file = path.Substring(rSegment + 3);
            if (file.Length != 16 + 5 || !file.EndsWith(".html", StringComparison.Ordinal))
            {
                return false;
            }

            symbolId = file.Substring(0, 16);
            for (int i = 0; i < symbolId.Length; i++)
            {
                char c = symbolId[i];
                bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!isHex)
                {
                    return false;
                }
            }

            return true;
        }

        private ReferencePack TryGetPack(string assembly)
        {
            var lazy = _packs.GetOrAdd(assembly, a => new Lazy<ReferencePack>(() =>
            {
                var referencesFolder = Path.Combine(_rootPath, a, Constants.ReferencesFileName);
                return ReferencePack.TryLoad(referencesFolder, out var pack) ? pack : null;
            }));

            return lazy.Value;
        }
    }
}

using System;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services
{
    public sealed class CoordinateMapper : ICoordinateMapper
    {
        private readonly NteRenderContextManager _ctx;


        public CoordinateMapper(NteRenderContextManager ctx)
        {
            _ctx = ctx;
        }

        public (int Px, int Py) LogicalToClient(IntPtr hwnd, int logicalX, int logicalY)
        {

            return _ctx.TranslateNtePointer(logicalX, logicalY);
        }
    }
}
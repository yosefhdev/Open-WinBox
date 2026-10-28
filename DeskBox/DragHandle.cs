using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace OpenWinBox;

// Agarradera para mover widgets sin barra de título; el cursor de "mover" deja claro para qué sirve.
// ProtectedCursor solo se puede asignar desde una subclase, por eso esta clase.
public sealed partial class DragHandle : Grid
{
    public DragHandle() => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
}

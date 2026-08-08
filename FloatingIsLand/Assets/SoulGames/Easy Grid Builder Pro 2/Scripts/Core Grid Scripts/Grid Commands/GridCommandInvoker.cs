using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace SoulGames.EasyGridBuilderPro
{
    public class GridCommandInvoker
    {
        private LinkedList<ICommand> undoCommandList;
        private Stack<ICommand> redoCommandStack;

        public GridCommandInvoker()
        {
            undoCommandList = new LinkedList<ICommand>();
            redoCommandStack = new Stack<ICommand>();
        }

        public void ExecuteCommand(ICommand command)
        {
            command.Execute();
        }

        public void AddCommand(ICommand command)
        {
            undoCommandList.AddLast(command);

            redoCommandStack.Clear(); // Clear the redo stack because a new command invalidates redo history
            if (undoCommandList.Count > GridManager.Instance.GetMaxUndoRedoCount()) undoCommandList.RemoveFirst(); // Removes the oldest (first) command
        }

        public void UndoCommand()
        {
            if (undoCommandList.Count > 0)
            {
                ICommand commandToUndo = undoCommandList.Last.Value;

                undoCommandList.RemoveLast();
                commandToUndo.Undo();

                redoCommandStack.Push(commandToUndo); // Push the undone command onto the redo stack
            }
        }

        public void RedoCommand()
        {
            if (redoCommandStack.Count > 0)
            {
                ICommand commandToRedo = redoCommandStack.Pop();
                commandToRedo.Redo();
                undoCommandList.AddLast(commandToRedo);
            }
        }

        public void SetUndoCommandLinkedList(LinkedList<ICommand> undoCommandList)
        {
            if (undoCommandList == null || undoCommandList.Count == 0) return;
            foreach (ICommand command in undoCommandList)
            {
                this.undoCommandList.AddLast(command);
            }
        }
        
        public void ClearUndoCommandLinkedList() => undoCommandList.Clear();  

        public void SetRedoCommandStack(Stack<ICommand> redoCommandStack)
        {
            if (redoCommandStack == null ||redoCommandStack.Count == 0) return;
            foreach (ICommand command in redoCommandStack)
            {
                this.redoCommandStack.Push(command);
            }
        }

        public void ClearRedoCommandStack() => redoCommandStack.Clear();  

        public LinkedList<ICommand> GetUndoCommandLinkedList() => undoCommandList;

        public Stack<ICommand> GetRedoCommandStack() => redoCommandStack;
    }
}
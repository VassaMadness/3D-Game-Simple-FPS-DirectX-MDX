Imports System.Threading

Public Class Form3
    Private game As GameClass1A
    Private gameThread As Thread

    Private Sub MainForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        'Me.FormBorderStyle = FormBorderStyle.None
        'Me.WindowState = FormWindowState.Maximized
        Me.Show()
        Me.Update()

        game = New GameClass1A()
        game.Init(Me, False)

        gameThread = New Thread(Sub()
                                    While Not game.GameOver
                                        game.RenderFrame()
                                    End While
                                End Sub)

        gameThread.IsBackground = True
        gameThread.Start()
    End Sub

    Private Sub MainForm_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        game?.ExitGame()
        gameThread?.Join(100)
    End Sub

    Private Sub MainForm_KeyDown(sender As Object, e As KeyEventArgs) Handles MyBase.KeyDown
        If e.KeyCode = Keys.F11 Then
            game.ToggleFullscreen()
        End If
    End Sub
End Class
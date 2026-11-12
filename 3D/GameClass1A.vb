Imports Microsoft.DirectX
Imports Microsoft.DirectX.Direct3D
Imports System.Threading
Imports System.Diagnostics

Public Class GameClass1A
    Private D3Dev As Device = Nothing
    Private D3Dpp As PresentParameters = Nothing
    Private ParentForm As Form = Nothing
    Public GameOver As Boolean = False
    Private FullScreen As Boolean = False

    ' === Cámara ===
    Private camPos As New Vector3(0, 1.7F, 0)
    Private camVel As New Vector3(0, 0, 0)
    Private yaw As Single = 0
    Private pitch As Single = 0
    Private moveSpeed As Single = 5.0F
    Private mouseSens As Single = -0.00005F

    ' === Física ===
    Private gravity As Single = 9.8F
    Private jumpPower As Single = 4.0F
    Private isOnGround As Boolean = False

    ' === Geometría (solo PositionColored) ===
    Private cieloVB As VertexBuffer
    Private floorVB As VertexBuffer
    Private wallVB(3) As VertexBuffer
    Private vertFormat As VertexFormats = VertexFormats.Position Or VertexFormats.Diffuse

    ' === Temporización ===
    Private sw As Stopwatch = Stopwatch.StartNew()
    Private lastFrameTime As Long = 0
    Private frameCount As Integer = 0
    Private lastFpsUpdate As Long = 0
    Public currentFPS As Integer = 0

    ' === Font para HUD ===
    Private dxFont As Font = Nothing

    ' === Input ===
    <Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function GetAsyncKeyState(vKey As Keys) As Short
    End Function

    Private lastMouseX As Integer = -1
    Private lastMouseY As Integer = -1

    ' === LUZ SIMULADA ===
    Private lightDir As Vector3 = New Vector3(-0.5F, -1.0F, -0.5F) ' Desde arriba y atrás

    Public Sub Init(ByVal form As Form, ByVal fullScreenMode As Boolean)
        ParentForm = form
        FullScreen = fullScreenMode

        Dim DP As DisplayMode = Manager.Adapters.Default.CurrentDisplayMode

        D3Dpp = New PresentParameters With {
            .Windowed = Not fullScreenMode,
            .SwapEffect = SwapEffect.Discard,
            .PresentationInterval = PresentInterval.Immediate,
            .BackBufferFormat = If(fullScreenMode, DP.Format, Format.Unknown),
            .BackBufferWidth = If(fullScreenMode, DP.Width, form.ClientSize.Width),
            .BackBufferHeight = If(fullScreenMode, DP.Height, form.ClientSize.Height),
            .EnableAutoDepthStencil = True,
            .AutoDepthStencilFormat = DepthFormat.D16
            }

        Try
            D3Dev = New Device(0, DeviceType.Hardware, form.Handle, CreateFlags.HardwareVertexProcessing Or CreateFlags.MultiThreaded, D3Dpp)
        Catch ex As Exception
            D3Dev = New Device(0, DeviceType.Hardware, form.Handle, CreateFlags.SoftwareVertexProcessing Or CreateFlags.MultiThreaded, D3Dpp)
        End Try

        ' === ¡DESACTIVAR ILUMINACIÓN DE DIRECTX! ===
        D3Dev.RenderState.Lighting = False
        D3Dev.RenderState.ZBufferEnable = True
        D3Dev.RenderState.CullMode = Cull.None

        'D3Dev.RenderState.FillMode = FillMode.WireFrame

        ' === Niebla oscura (opcional) ===
        D3Dev.RenderState.FogEnable = True
        D3Dev.RenderState.FogColor = Color.FromArgb(10, 10, 10)
        D3Dev.RenderState.FogStart = 0.0F
        D3Dev.RenderState.FogEnd = 35.0F
        D3Dev.RenderState.FogDensity = 500.0F
        D3Dev.RenderState.FogTableMode = FogMode.Linear

        InitGeometry()
        SetupMatrices()

        Try
            dxFont = New Font(D3Dev, New System.Drawing.Font("Courier New", 12, FontStyle.Bold))
        Catch
        End Try

        If FullScreen Then
            Cursor.Hide()
            CenterCursor()
        Else
            Dim screenPoint = ParentForm.PointToScreen(New Point(ParentForm.ClientSize.Width \ 2, ParentForm.ClientSize.Height \ 2))
            lastMouseX = screenPoint.X
            lastMouseY = screenPoint.Y
        End If
    End Sub

    Private Function GetLitColor(baseColor As Color, normal As Vector3) As Integer
        Dim lightNorm As Vector3 = lightDir
        lightNorm.Normalize()
        Dim dot = Vector3.Dot(normal, lightNorm)

        ' Solo luz difusa cuando la normal mira hacia la luz
        If dot < 0 Then dot = 0

        ' Combinar: 40% ambiental + 60% difusa
        Dim intensity As Single = 0.94F + dot * 1000000.63F

        Dim r As Integer = CInt(Math.Min(255, baseColor.R * intensity))
        Dim g As Integer = CInt(Math.Min(255, baseColor.G * intensity))
        Dim b As Integer = CInt(Math.Min(255, baseColor.B * intensity))

        Return Color.FromArgb(r, g, b).ToArgb()
    End Function

    Private Sub InitGeometry()
        ' Piso: normales hacia arriba (0,1,0)
        Dim floorVerts() As CustomVertex.PositionColored = {
            New CustomVertex.PositionColored(-10, 0, -10, GetLitColor(Color.DarkGreen, New Vector3(0, 1, 0))),
            New CustomVertex.PositionColored(10, 0, -10, GetLitColor(Color.DarkGreen, New Vector3(0, 1, 0))),
            New CustomVertex.PositionColored(-10, 0, 10, GetLitColor(Color.DarkGreen, New Vector3(0, 1, 0))),
            New CustomVertex.PositionColored(10, 0, -10, GetLitColor(Color.DarkGreen, New Vector3(0, 1, 0))),
            New CustomVertex.PositionColored(10, 0, 10, GetLitColor(Color.DarkGreen, New Vector3(0, 1, 0))),
            New CustomVertex.PositionColored(-10, 0, 10, GetLitColor(Color.DarkGreen, New Vector3(0, 1, 0)))
        }

        floorVB = New VertexBuffer(GetType(CustomVertex.PositionColored), 6, D3Dev, Usage.WriteOnly, vertFormat, Pool.Default)
        floorVB.SetData(floorVerts, 0, LockFlags.None)


        Dim cieloVerts() As CustomVertex.PositionColored = {
    New CustomVertex.PositionColored(-100, 5, -100, GetLitColor(Color.Blue, New Vector3(0, -1, 0))),
    New CustomVertex.PositionColored(100, 5, -100, GetLitColor(Color.Blue, New Vector3(0, -1, 0))),
    New CustomVertex.PositionColored(-100, 5, 100, GetLitColor(Color.Blue, New Vector3(0, -1, 0))),
    New CustomVertex.PositionColored(100, 5, -100, GetLitColor(Color.Blue, New Vector3(0, -1, 0))),
    New CustomVertex.PositionColored(100, 5, 100, GetLitColor(Color.Blue, New Vector3(0, -1, 0))),
    New CustomVertex.PositionColored(-100, 5, 100, GetLitColor(Color.Blue, New Vector3(0, -1, 0)))
}

        cieloVB = New VertexBuffer(GetType(CustomVertex.PositionColored), 6, D3Dev, Usage.WriteOnly, vertFormat, Pool.Default)
        cieloVB.SetData(cieloVerts, 0, LockFlags.None)


        ' Paredes
        Dim walls(3)() As CustomVertex.PositionColored

        ' Atrás (-Z)
        walls(0) = {
            New CustomVertex.PositionColored(-10, 0, -10, GetLitColor(Color.Red, New Vector3(0, 0, -1))),
            New CustomVertex.PositionColored(10, 0, -10, GetLitColor(Color.Red, New Vector3(0, 0, -1))),
            New CustomVertex.PositionColored(-10, 3, -10, GetLitColor(Color.Red, New Vector3(0, 0, -1))),
            New CustomVertex.PositionColored(10, 3, -10, GetLitColor(Color.Red, New Vector3(0, 0, -1)))
        }

        ' Frente (+Z)
        walls(1) = {
            New CustomVertex.PositionColored(-10, 0, 10, GetLitColor(Color.Blue, New Vector3(0, 0, 1))),
            New CustomVertex.PositionColored(10, 0, 10, GetLitColor(Color.Blue, New Vector3(0, 0, 1))),
            New CustomVertex.PositionColored(-10, 3, 10, GetLitColor(Color.Blue, New Vector3(0, 0, 1))),
            New CustomVertex.PositionColored(10, 3, 10, GetLitColor(Color.Blue, New Vector3(0, 0, 1)))
        }

        ' Izquierda (-X)
        walls(2) = {
            New CustomVertex.PositionColored(-10, 0, -10, GetLitColor(Color.Yellow, New Vector3(-1, 0, 0))),
            New CustomVertex.PositionColored(-10, 0, 10, GetLitColor(Color.Yellow, New Vector3(-1, 0, 0))),
            New CustomVertex.PositionColored(-10, 3, -10, GetLitColor(Color.Yellow, New Vector3(-1, 0, 0))),
            New CustomVertex.PositionColored(-10, 3, 10, GetLitColor(Color.Yellow, New Vector3(-1, 0, 0)))
        }

        ' Derecha (+X)
        walls(3) = {
            New CustomVertex.PositionColored(10, 0, -10, GetLitColor(Color.Orange, New Vector3(1, 0, 0))),
            New CustomVertex.PositionColored(10, 0, 10, GetLitColor(Color.Orange, New Vector3(1, 0, 0))),
            New CustomVertex.PositionColored(10, 3, -10, GetLitColor(Color.Orange, New Vector3(1, 0, 0))),
            New CustomVertex.PositionColored(10, 3, 10, GetLitColor(Color.Orange, New Vector3(1, 0, 0)))
        }

        For i As Integer = 0 To 3
            wallVB(i) = New VertexBuffer(GetType(CustomVertex.PositionColored), 4, D3Dev, Usage.WriteOnly, vertFormat, Pool.Default)
            wallVB(i).SetData(walls(i), 0, LockFlags.None)
        Next
    End Sub

    Private Sub SetupMatrices()
        If D3Dev Is Nothing Then Return
        Dim w As Integer = If(FullScreen, D3Dpp.BackBufferWidth, ParentForm.ClientSize.Width)
        Dim h As Integer = If(FullScreen, D3Dpp.BackBufferHeight, ParentForm.ClientSize.Height)
        Dim aspect As Single = CSng(w) / h
        D3Dev.Transform.Projection = Matrix.PerspectiveFovLH(Math.PI / 3.0F, aspect, 0.1F, 100.0F)
    End Sub

    Public Sub RenderFrame()
        If GameOver OrElse D3Dev Is Nothing Then Return

        Dim now = sw.ElapsedMilliseconds
        Dim delta = (now - lastFrameTime) / 1000.0F
        lastFrameTime = now
        If delta > 0.1F Then delta = 0.1F

        UpdatePhysics(delta)
        HandleInput(delta)
        UpdateCamera()

        Try
            ' === Fondo azul cielo ===
            D3Dev.Clear(ClearFlags.Target Or ClearFlags.ZBuffer, Color.CornflowerBlue, 1.0F, 0)
            D3Dev.BeginScene()
            D3Dev.VertexFormat = vertFormat

            '' techo visuales
            'D3Dev.SetStreamSource(0, cieloVB, 0)
            'D3Dev.DrawPrimitives(PrimitiveType.TriangleList, 0, 2)

            D3Dev.SetStreamSource(0, floorVB, 0)
            D3Dev.DrawPrimitives(PrimitiveType.TriangleList, 0, 2)

            For i As Integer = 0 To 3
                D3Dev.SetStreamSource(0, wallVB(i), 0)
                D3Dev.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2)
            Next

            ' === HUD ===
            If dxFont IsNot Nothing Then
                Dim y As Integer = 10
                dxFont.DrawText(Nothing, $"FPS: {currentFPS}", 10, y, Color.White) : y += 20
                dxFont.DrawText(Nothing, $"Pos: X={camPos.X:F2}, Y={camPos.Y:F2}, Z={camPos.Z:F2}", 10, y, Color.Yellow) : y += 20
                'dxFont.DrawText(Nothing, $"Yaw: {yaw * 180 / Math.PI:F1}° | Pitch: {pitch * 180 / Math.PI:F1}°", 10, y, Color.Cyan) : y += 20
                'dxFont.DrawText(Nothing, $"Yaw: {yaw * 380 / Math.PI:F1}° | Pitch: {pitch * 180 / Math.PI:F1}°", 10, y, Color.Cyan) : y += 20
                dxFont.DrawText(Nothing, $"Mode: {(If(FullScreen, "Fullscreen", "Windowed"))}", 10, y, Color.Green) : y += 20
                'dxFont.DrawText(Nothing, $"Ground: {(If(isOnGround, "YES", "NO"))}", 10, y, Color.Magenta)
            End If

            D3Dev.EndScene()
            D3Dev.Present()

            frameCount += 1
            If now - lastFpsUpdate >= 1000 Then
                currentFPS = frameCount
                frameCount = 0
                lastFpsUpdate = now
            End If

        Catch ex As DeviceLostException
            'Thread.Sleep(20)
            Try
                D3Dev.Reset(D3Dpp)
                InitGeometry()
                SetupMatrices()
                dxFont?.Dispose()
                dxFont = New Font(D3Dev, New System.Drawing.Font("Courier New", 12, FontStyle.Bold))
                If FullScreen Then
                    CenterCursor()
                Else
                    Dim screenPoint = ParentForm.PointToScreen(New Point(ParentForm.ClientSize.Width \ 2, ParentForm.ClientSize.Height \ 2))
                    lastMouseX = screenPoint.X
                    lastMouseY = screenPoint.Y
                End If
            Catch
                GameOver = True
            End Try
        End Try
    End Sub

    Private Sub UpdatePhysics(deltaTime As Single)
        If Not isOnGround Then
            camVel.Y -= gravity * deltaTime
        End If

        camPos += camVel * deltaTime

        If camPos.Y < 1.7F Then
            camPos.Y = 1.7F
            camVel.Y = 0
            isOnGround = True
        Else
            isOnGround = False
        End If

        ' techo limite colision
        Dim ceilingHeight As Single = 25.0F
        If camPos.Y > ceilingHeight - 0.2F Then
            camPos.Y = ceilingHeight - 0.2F
            camVel.Y = 0
        End If

        camPos.X = Math.Max(-9.5F, Math.Min(9.5F, camPos.X))
        camPos.Z = Math.Max(-9.5F, Math.Min(9.5F, camPos.Z))

        camVel.X *= 0.9F
        camVel.Z *= 0.9F
    End Sub

    Private Sub HandleInput(deltaTime As Single)
        Dim forward As New Vector3(Math.Sin(yaw), 0, -Math.Cos(yaw))
        Dim right As New Vector3(Math.Cos(yaw), 0, Math.Sin(yaw))

        If isOnGround Then
            camVel.X = 0
            camVel.Z = 0
        End If

        If (GetAsyncKeyState(Keys.W) And &H8000) <> 0 Then camVel.X += forward.X * moveSpeed : camVel.Z += forward.Z * moveSpeed
        If (GetAsyncKeyState(Keys.S) And &H8000) <> 0 Then camVel.X -= forward.X * moveSpeed : camVel.Z -= forward.Z * moveSpeed
        If (GetAsyncKeyState(Keys.A) And &H8000) <> 0 Then camVel.X += right.X * moveSpeed : camVel.Z += right.Z * moveSpeed
        If (GetAsyncKeyState(Keys.D) And &H8000) <> 0 Then camVel.X -= right.X * moveSpeed : camVel.Z -= right.Z * moveSpeed

        If (GetAsyncKeyState(Keys.Space) And &H8000) <> 0 AndAlso isOnGround Then
            camVel.Y = jumpPower
            isOnGround = False
        End If

        If (GetAsyncKeyState(Keys.Escape) And &H8000) <> 0 Then GameOver = True

        If FullScreen Then
            Dim cx = D3Dpp.BackBufferWidth \ 2
            Dim cy = D3Dpp.BackBufferHeight \ 2
            Dim dx = Cursor.Position.X - cx
            Dim dy = Cursor.Position.Y - cy
            If dx <> 0 OrElse dy <> 0 Then
                yaw += dx * mouseSens
                pitch += dy * mouseSens
                pitch = Math.Max(-1.5F, Math.Min(1.5F, pitch))
                Cursor.Position = New Point(cx, cy)
            End If
        Else
            Dim x = Cursor.Position.X
            Dim y = Cursor.Position.Y
            If lastMouseX = -1 Then lastMouseX = x : lastMouseY = y
            Dim dx = x - lastMouseX
            Dim dy = y - lastMouseY
            If dx <> 0 OrElse dy <> 0 Then
                yaw += dx * mouseSens
                pitch += dy * mouseSens
                pitch = Math.Max(-1.5F, Math.Min(1.5F, pitch))
            End If
            lastMouseX = x
            lastMouseY = y
        End If
    End Sub

    Private Sub CenterCursor()
        If FullScreen Then
            Cursor.Position = New Point(D3Dpp.BackBufferWidth \ 2, D3Dpp.BackBufferHeight \ 2)
        End If
    End Sub

    Private Sub UpdateCamera()
        If D3Dev Is Nothing Then Return
        Dim lookDir As New Vector3(
            Math.Sin(yaw) * Math.Cos(pitch),
            Math.Sin(pitch),
            -Math.Cos(yaw) * Math.Cos(pitch)
        )
        D3Dev.Transform.View = Matrix.LookAtLH(camPos, camPos + lookDir, New Vector3(0, 1, 0))
    End Sub

    Public Sub ToggleFullscreen()
        FullScreen = Not FullScreen
        Dim DP = Manager.Adapters.Default.CurrentDisplayMode
        D3Dpp.Windowed = Not FullScreen
        D3Dpp.BackBufferWidth = If(FullScreen, DP.Width, ParentForm.ClientSize.Width)
        D3Dpp.BackBufferHeight = If(FullScreen, DP.Height, ParentForm.ClientSize.Height)

        Try
            D3Dev.Reset(D3Dpp)
            SetupMatrices()
            dxFont?.Dispose()
            dxFont = New Font(D3Dev, New System.Drawing.Font("Courier New", 12, FontStyle.Bold))
            If FullScreen Then
                Cursor.Hide()
                CenterCursor()
            Else
                Cursor.Show()
                Dim p = ParentForm.PointToScreen(New Point(ParentForm.ClientSize.Width \ 2, ParentForm.ClientSize.Height \ 2))
                lastMouseX = p.X
                lastMouseY = p.Y
            End If
        Catch
            GameOver = True
        End Try
    End Sub

    Public Sub ExitGame()
        GameOver = True
        If FullScreen Then Cursor.Show()
        Try
            dxFont?.Dispose()
            floorVB?.Dispose()
            For Each vb In wallVB
                vb?.Dispose()
            Next
            D3Dev?.Dispose()
        Catch
        End Try
    End Sub
End Class
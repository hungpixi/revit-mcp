@echo off
echo ==============================================
echo Installing Revit MCP Node dependencies...
echo ==============================================
call npm install

echo.
echo ==============================================
echo Building the TypeScript codebase...
echo ==============================================
call npm run build

echo.
echo ==============================================
echo Setup Complete!
echo You can now use the Revit MCP server.
echo ==============================================
pause

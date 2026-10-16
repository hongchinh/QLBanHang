#!/bin/bash

# Script để chạy cả backend (.NET) và frontend (Vite/React)

echo "Khoi dong he thong QL Don Hang..."

# Kiem tra dotnet
if ! command -v dotnet &> /dev/null; then
    echo "LÔII: .NET SDK chua duoc cai dat. Vui long cai dat .NET 10 SDK"
    exit 1
fi

# Kiem tra node
if ! command -v node &> /dev/null; then
    echo "LÔII: Node.js chua duoc cai dat. Vui long cai dat Node.js 18+"
    exit 1
fi

# Kiem tra npm
if ! command -v npm &> /dev/null; then
    echo "LÔII: npm chua duoc cai dat."
    exit 1
fi

echo "Dependencies san sang."

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_DIR="$SCRIPT_DIR/backend/src/OrderMgmt.WebApi"
FRONTEND_DIR="$SCRIPT_DIR/frontend"

# Cai frontend dependencies neu chua co
if [ ! -d "$FRONTEND_DIR/node_modules" ]; then
    echo "Cai dat frontend dependencies..."
    cd "$FRONTEND_DIR" && npm install
fi

echo "Khoi dong servers..."

# Chay .NET backend trong background
echo "Backend: http://localhost:5050  (Swagger: http://localhost:5050/swagger)"
cd "$BACKEND_DIR"
dotnet run --no-launch-profile --urls "http://localhost:5050" &
BACKEND_PID=$!

# Cho backend khoi dong
sleep 4

# Chay Vite frontend trong background
echo "Frontend: http://localhost:5173"
cd "$FRONTEND_DIR"
npm run dev &
FRONTEND_PID=$!

echo ""
echo "He thong da khoi dong!"
echo ""
echo "  Frontend : http://localhost:5173"
echo "  Backend  : http://localhost:5050"
echo "  Swagger  : http://localhost:5050/swagger"
echo ""
echo "Nhan Ctrl+C de dung ca hai servers."

# Cleanup khi nhan Ctrl+C
cleanup() {
    echo ""
    echo "Dang dung servers..."
    kill "$BACKEND_PID" "$FRONTEND_PID" 2>/dev/null
    wait "$BACKEND_PID" "$FRONTEND_PID" 2>/dev/null
    echo "Da dung."
    exit 0
}

trap cleanup INT TERM

wait

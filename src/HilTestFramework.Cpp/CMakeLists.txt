cmake_minimum_required(VERSION 3.20)
project(HilTestFramework VERSION 1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# ---------------------------------------------------------------------------
# Google Test via FetchContent — no separate install needed
# ---------------------------------------------------------------------------
include(FetchContent)
FetchContent_Declare(
  googletest
  URL https://github.com/google/googletest/archive/v1.14.0.zip
  DOWNLOAD_EXTRACT_TIMESTAMP TRUE
)
# Prevent overriding the parent project's compiler/linker settings on Windows
set(gtest_force_shared_crt ON CACHE BOOL "" FORCE)
FetchContent_MakeAvailable(googletest)

# ---------------------------------------------------------------------------
# HilCore — interface headers only (INTERFACE library, nothing to compile)
# ---------------------------------------------------------------------------
add_library(HilCore INTERFACE)
target_include_directories(HilCore INTERFACE
  ${CMAKE_CURRENT_SOURCE_DIR}/interfaces
)

# ---------------------------------------------------------------------------
# HilMocks — mock implementations for CI (no hardware required)
# ---------------------------------------------------------------------------
add_library(HilMocks STATIC
  mocks/MockUartDevice.cpp
  mocks/MockSaleaeClient.cpp
  mocks/MockFaultInjector.cpp
)
target_include_directories(HilMocks PUBLIC
  ${CMAKE_CURRENT_SOURCE_DIR}/mocks
)
target_link_libraries(HilMocks PUBLIC HilCore)

# ---------------------------------------------------------------------------
# HilTests — Google Test executable
# ---------------------------------------------------------------------------
add_executable(HilTests
  tests/NormalOperationTests.cpp
  tests/UartFaultTests.cpp
)
target_link_libraries(HilTests PRIVATE
  HilMocks
  GTest::gtest_main
)

# Auto-discover tests for CTest and Visual Studio Test Explorer
include(GoogleTest)
gtest_discover_tests(HilTests)

# ---------------------------------------------------------------------------
# CTest integration
# ---------------------------------------------------------------------------
enable_testing()

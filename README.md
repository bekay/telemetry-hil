# HIL Test Framework

> May 2026 —

## Project Overview

A hardware-in-the-loop (HIL) test framework spanning bare-metal embedded firmware through
cloud observability.

The system is real hardware running real firmware with real protocol communication, not a
simulation but a simulation in some places to make it interesting. There will be fault detection
through real physical signal injection rather than software generated faults.

## Current Hardware Inventory

| Device | Role | Status |
|--------|------|--------|
| x86_64 NixOS Workstation (VMware, Bridged) | K3s master (amd64), data services | Active |
| x86_64 NixOS Workstation | K3s agent (amd64), Logic 2 host, hardware attached | Active |
| Saleae Logic 16 (digital only) | Ground truth signal capture on I2C and UART | Active |
| Silicon Labs EFM32 Pearl Gecko | FreeRTOS, I2C sensor, UART output | In Progress |
| Microchip Microstick II | USB-controlled fault injector — sits on UART line, injects physical faults | In Progress |
| I2C sensor breakout | Real discrete sensor on Pearl Gecko I2C bus | In Progress |

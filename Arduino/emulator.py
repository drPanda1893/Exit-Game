"""
Big Yahu – Arduino Emulator
============================
Läuft in VSCode Terminal. Verbindet sich via TCP mit Unity (ArduinoBridge TCP-Modus).

Voraussetzung in Unity:
  ArduinoBridge GameObject → Inspector:
    [x] Use Tcp Emulator
    Tcp Port: 12345

Starten:
  python Arduino/emulator.py

Befehle im Terminal:
  0-9        → KEY:0 … KEY:9
  d / del    → KEY:DEL
  e / ent    → KEY:ENT
  h <0-100>  → HUMIDITY:<wert>    Beispiel:  h 75
  c <name>   → COLOR:<name>       Beispiel:  c RED
  q / quit   → Beenden
"""

import socket
import sys

HOST = "127.0.0.1"
PORT = 12345

HELP = """
┌─────────────────────────────────────────────────┐
│         Big Yahu – Arduino Emulator             │
├─────────────────────────────────────────────────┤
│  0-9          Keypad-Taste                      │
│  d / del      KEY:DEL                           │
│  e / ent      KEY:ENT                           │
│  h <0-100>    HUMIDITY:<wert>   z.B. h 75       │
│  c <name>     COLOR:<name>      z.B. c RED      │
│  ?            Diese Hilfe                       │
│  q / quit     Beenden                           │
└─────────────────────────────────────────────────┘
"""

def send(sock, message: str):
    line = message.strip() + "\n"
    sock.sendall(line.encode("utf-8"))
    print(f"  → {message}")

def parse_input(text: str, sock) -> bool:
    """Returns False when user wants to quit."""
    parts = text.strip().split()
    if not parts:
        return True

    cmd = parts[0].lower()

    if cmd in ("q", "quit", "exit"):
        return False

    if cmd in ("?", "help"):
        print(HELP)
        return True

    # Digits 0-9
    if len(cmd) == 1 and cmd.isdigit():
        send(sock, f"KEY:{cmd}")
        return True

    if cmd in ("d", "del", "delete"):
        send(sock, "KEY:DEL")
        return True

    if cmd in ("e", "ent", "enter"):
        send(sock, "KEY:ENT")
        return True

    if cmd == "h":
        if len(parts) < 2:
            print("  Bitte Wert angeben, z.B.:  h 75")
            return True
        try:
            val = int(parts[1])
            if not 0 <= val <= 100:
                raise ValueError
            send(sock, f"HUMIDITY:{val}")
        except ValueError:
            print("  Ungültiger Wert – bitte 0-100 angeben")
        return True

    if cmd == "c":
        if len(parts) < 2:
            print("  Bitte Farbe angeben, z.B.:  c RED")
            return True
        color = parts[1].upper()
        send(sock, f"COLOR:{color}")
        return True

    print(f"  Unbekannter Befehl: '{text}'  (? für Hilfe)")
    return True


def main():
    print(f"\nVerbinde mit Unity auf {HOST}:{PORT} …")
    print("(Stelle sicher, dass Unity im Play-Mode läuft und 'Use Tcp Emulator' aktiv ist)\n")

    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.connect((HOST, PORT))
    except ConnectionRefusedError:
        print("Verbindung fehlgeschlagen – ist Unity im Play-Mode mit aktiviertem TCP-Emulator?")
        sys.exit(1)

    print("Verbunden!\n")
    print(HELP)

    try:
        while True:
            try:
                text = input("emulator> ").strip()
            except EOFError:
                break
            if not parse_input(text, sock):
                break
    except KeyboardInterrupt:
        pass
    finally:
        sock.close()
        print("\nVerbindung getrennt.")


if __name__ == "__main__":
    main()

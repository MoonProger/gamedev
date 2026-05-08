import { useEffect, useRef, useCallback, useState } from 'react';
import { api } from '../services/api';
import { WsClientToServer, WsServerToClient } from '../types/ws-protocol';

export const useWebSocket = (
  roomId: string | null,
  roomPassword: string | null,
  onMessage: (data: WsServerToClient) => void
) => {
  const wsRef = useRef<WebSocket | null>(null);
  const roomIdRef = useRef(roomId);
  const roomPasswordRef = useRef(roomPassword);
  const isJoinedRef = useRef(false);
  const [isConnected, setIsConnected] = useState(false);

  useEffect(() => {
    roomIdRef.current = roomId;
    roomPasswordRef.current = roomPassword;
  }, [roomId, roomPassword]);

  useEffect(() => {
    if (!roomId) return;

    let isMounted = true;

    const connect = () => {
      try {
        const ws = api.connectWebSocket((data: WsServerToClient) => {
          if (isMounted) {
            onMessage(data);
          }
        });
        wsRef.current = ws;

        ws.onopen = () => {
          console.log('WebSocket connected');
          setIsConnected(true);
          if (isMounted && roomIdRef.current && !isJoinedRef.current) {
            console.log('Sending room.join with:', { 
              roomId: roomIdRef.current, 
              password: roomPasswordRef.current 
            });
            ws.send(JSON.stringify({
              type: 'room.join',
              payload: { 
                roomId: roomIdRef.current,
                password: roomPasswordRef.current || undefined
              }
            }));
            isJoinedRef.current = true;
          }
        };

        ws.onclose = () => {
          console.log('WebSocket disconnected');
          setIsConnected(false);
          isJoinedRef.current = false;
        };

        ws.onerror = (error) => {
          console.error('WebSocket error:', error);
        };
      } catch (error) {
        console.error('Failed to connect WebSocket:', error);
      }
    };

    connect();

    return () => {
      isMounted = false;
      if (wsRef.current?.readyState === WebSocket.OPEN) {
        wsRef.current.close();
      }
      setIsConnected(false);
      isJoinedRef.current = false;
    };
  }, [roomId, onMessage]);

  const send = useCallback((message: WsClientToServer) => {
    if (wsRef.current?.readyState === WebSocket.OPEN) {
      wsRef.current.send(JSON.stringify(message));
    } else {
      console.warn('WebSocket not connected, cannot send:', message.type);
    }
  }, []);

  return { send, isConnected };
};
import { useEffect, useRef, useCallback, useState } from 'react';
import { api } from '../services/api';

export const useWebSocket = (
  roomId: string | null,
  roomPassword: string | null,
  onMessage: (data: any) => void,
  skipJoin: boolean = false
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
        const ws = api.connectWebSocket((data) => {
          if (isMounted) {
            onMessage(data);
          }
        });
        wsRef.current = ws;

        ws.onopen = () => {
          console.log('WebSocket connected');
          setIsConnected(true);
          if (isMounted && roomIdRef.current && !isJoinedRef.current && !skipJoin) {
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
          } else {
            console.log('Skipping room.join (skipJoin=true or already joined)');
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
  }, [roomId, onMessage, skipJoin]);

  const send = useCallback((type: string, payload: any) => {
    if (wsRef.current?.readyState === WebSocket.OPEN) {
      wsRef.current.send(JSON.stringify({ type, payload }));
    } else {
      console.warn('WebSocket not connected, cannot send:', type);
    }
  }, []);

  return { send, isConnected };
};
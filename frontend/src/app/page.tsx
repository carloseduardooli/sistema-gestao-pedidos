'use client';

import { useState, useRef, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';

type Order = {
  id: string;
  cliente: string;
  produto: string;
  valor: number;
  status: number;
  dataCriacao: string;
};

const API_URL = 'http://localhost:5000/orders';

export default function Home() {
  const queryClient = useQueryClient();
  const [mounted, setMounted] = useState(false);

  useEffect(() => { setMounted(true); }, []);
  
  // Estados do Pedido
  const [cliente, setCliente] = useState('');
  const [produto, setProduto] = useState('');
  const [valor, setValor] = useState('');

  // Estados da IA
  const [question, setQuestion] = useState('');
  const [chatHistory, setChatHistory] = useState<{role: 'user' | 'ai', text: string}[]>([
    { role: 'ai', text: 'Olá! Sou a IA assistente deste painel. O que você gostaria de analisar nos pedidos?' }
  ]);
  const chatEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [chatHistory]);

  const { data: orders = [], isLoading } = useQuery<Order[]>({
    queryKey: ['orders'],
    queryFn: async () => {
      const res = await fetch(API_URL);
      if (!res.ok) throw new Error('Erro ao buscar pedidos');
      return res.json();
    },
    refetchInterval: 3000 // Tempo real via fallback (Polling)
  });

  const createOrder = useMutation({
    mutationFn: async (newOrder: { cliente: string; produto: string; valor: number }) => {
      const res = await fetch(API_URL, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(newOrder),
      });
      if (!res.ok) throw new Error('Erro ao criar pedido');
      return res.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['orders'] });
      setCliente('');
      setProduto('');
      setValor('');
    }
  });

  const askAi = useMutation({
    mutationFn: async (q: string) => {
      await new Promise(resolve => setTimeout(resolve, 600)); 
      const res = await fetch(`${API_URL}/ask`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ question: q }),
      });
      if (!res.ok) throw new Error('Erro ao consultar IA');
      return res.json();
    },
    onSuccess: (data) => {
      setChatHistory(prev => [...prev, { role: 'ai', text: data.resposta }]);
    },
    onError: () => {
      setChatHistory(prev => [...prev, { role: 'ai', text: '❌ Erro de comunicação com a API.' }]);
    }
  });

  const handleSubmitOrder = (e: React.FormEvent) => {
    e.preventDefault();
    if (!cliente || !produto || !valor) return;
    createOrder.mutate({ cliente, produto, valor: parseFloat(valor) });
  };

  const handleAskAi = (e: React.FormEvent) => {
    e.preventDefault();
    if (!question.trim() || askAi.isPending) return;
    
    setChatHistory(prev => [...prev, { role: 'user', text: question }]);
    askAi.mutate(question);
    setQuestion('');
  };

  const getStatusBadge = (status: number) => {
    switch (status) {
      case 1: return <span className="px-2.5 py-1 bg-amber-500/10 text-amber-400 border border-amber-500/20 rounded-md text-xs font-medium">Pendente</span>;
      case 2: return <span className="px-2.5 py-1 bg-blue-500/10 text-blue-400 border border-blue-500/20 rounded-md text-xs font-medium animate-pulse">Processando</span>;
      case 3: return <span className="px-2.5 py-1 bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 rounded-md text-xs font-medium">Finalizado</span>;
      default: return <span>Desconhecido</span>;
    }
  };

  return (
    <div className="min-h-screen p-8 max-w-7xl mx-auto flex flex-col gap-6 font-sans">
      
      {/* Header */}
      <header className="flex justify-between items-end mb-2">
        <div>
          <h1 className="text-3xl font-bold text-white tracking-tight">Painel de Pedidos</h1>
          <p className="text-slate-400 text-sm mt-1">Gerenciamento e Análise em Tempo Real</p>
        </div>
        <div className="flex items-center gap-2 bg-emerald-500/10 px-3 py-1.5 rounded-lg border border-emerald-500/20">
          <div className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></div>
          <span className="text-xs font-semibold text-emerald-400 uppercase tracking-wide">Sync Ativo</span>
        </div>
      </header>

      {/* Barra superior de Criação (Inline Form) */}
      <div className="bg-slate-900 p-5 rounded-2xl border border-slate-800 shadow-xl">
        <form onSubmit={handleSubmitOrder} className="flex flex-col md:flex-row gap-4 items-end">
          <div className="flex-1 w-full">
            <label className="block text-xs font-medium text-slate-400 mb-1.5 uppercase tracking-wide">Cliente</label>
            <input type="text" value={cliente} onChange={e => setCliente(e.target.value)} className="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-slate-200 text-sm focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 outline-none transition-all" placeholder="Nome do cliente" required />
          </div>
          <div className="flex-1 w-full">
            <label className="block text-xs font-medium text-slate-400 mb-1.5 uppercase tracking-wide">Produto</label>
            <input type="text" value={produto} onChange={e => setProduto(e.target.value)} className="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-slate-200 text-sm focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 outline-none transition-all" placeholder="Item comprado" required />
          </div>
          <div className="w-full md:w-48">
            <label className="block text-xs font-medium text-slate-400 mb-1.5 uppercase tracking-wide">Valor (R$)</label>
            <input type="number" step="0.01" value={valor} onChange={e => setValor(e.target.value)} className="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-slate-200 text-sm focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 outline-none transition-all" placeholder="0.00" required />
          </div>
          <button suppressHydrationWarning type="submit" disabled={createOrder.isPending} className="w-full md:w-auto bg-indigo-600 text-white px-6 py-2.5 rounded-lg text-sm font-semibold hover:bg-indigo-500 disabled:opacity-50 transition-colors h-[42px]">
            {createOrder.isPending ? 'Enviando...' : 'Criar Pedido'}
          </button>
        </form>
      </div>

      {/* Grid Principal: Tabela (Esquerda) e Chat (Direita) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 h-[600px]">
        
        {/* Tabela de Pedidos (7 colunas) */}
        <div className="lg:col-span-7 bg-slate-900 rounded-2xl border border-slate-800 shadow-xl flex flex-col overflow-hidden">
          <div className="p-5 border-b border-slate-800 bg-slate-900/50">
            <h2 className="text-lg font-semibold text-slate-200">Últimos Lançamentos</h2>
          </div>
          
          <div className="flex-1 overflow-y-auto p-0">
            {isLoading ? (
              <div className="flex items-center justify-center h-full text-slate-500 text-sm animate-pulse">Sincronizando dados...</div>
            ) : (
              <table className="min-w-full divide-y divide-slate-800/50">
                <thead className="bg-slate-950/50 sticky top-0 backdrop-blur-sm z-10">
                  <tr>
                    <th className="px-5 py-4 text-left text-[11px] font-bold text-slate-500 uppercase tracking-wider">Cliente</th>
                    <th className="px-5 py-4 text-left text-[11px] font-bold text-slate-500 uppercase tracking-wider">Produto</th>
                    <th className="px-5 py-4 text-left text-[11px] font-bold text-slate-500 uppercase tracking-wider">Valor</th>
                    <th className="px-5 py-4 text-left text-[11px] font-bold text-slate-500 uppercase tracking-wider">Status</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800/50">
                  {orders.map((order) => (
                    <tr key={order.id} className="hover:bg-slate-800/30 transition-colors group">
                      <td className="px-5 py-4 whitespace-nowrap text-sm font-medium text-slate-200">{order.cliente}</td>
                      <td className="px-5 py-4 whitespace-nowrap text-sm text-slate-400 group-hover:text-slate-300">{order.produto}</td>
                      <td className="px-5 py-4 whitespace-nowrap text-sm font-semibold text-slate-300">
                        {new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(order.valor)}
                      </td>
                      <td className="px-5 py-4 whitespace-nowrap">
                        {getStatusBadge(order.status)}
                      </td>
                    </tr>
                  ))}
                  {orders.length === 0 && (
                    <tr>
                      <td colSpan={4} className="px-5 py-12 text-center text-sm text-slate-500">A fila de pedidos está vazia.</td>
                    </tr>
                  )}
                </tbody>
              </table>
            )}
          </div>
        </div>

        {/* Chat de IA (5 colunas) */}
        <div className="lg:col-span-5 bg-slate-900 rounded-2xl border border-slate-800 shadow-xl flex flex-col overflow-hidden relative">
          
          <div className="p-4 border-b border-slate-800 bg-slate-900/80 flex items-center gap-3 shrink-0">
            <div className="w-8 h-8 rounded-full bg-gradient-to-tr from-indigo-500 to-purple-500 flex items-center justify-center shadow-lg shadow-indigo-500/20">
              <svg className="w-4 h-4 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" /></svg>
            </div>
            <div>
              <h2 className="text-sm font-bold text-slate-200">IA Analytics</h2>
              <p className="text-[11px] text-slate-400">Assistente</p>
            </div>
          </div>
          
          <div className="flex-1 overflow-y-auto p-5 space-y-5 bg-slate-950/30">
            {chatHistory.map((msg, index) => (
              <div key={index} className={`flex ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}>
                <div className={`max-w-[85%] rounded-2xl p-3.5 text-[13px] leading-relaxed shadow-sm whitespace-pre-wrap ${
                  msg.role === 'user' 
                  ? 'bg-indigo-600 text-indigo-50 rounded-br-sm' 
                  : 'bg-slate-800 border border-slate-700 text-slate-300 rounded-bl-sm'
                }`}>
                  {msg.text}
                </div>
              </div>
            ))}
            
            {askAi.isPending && (
              <div className="flex justify-start">
                <div className="bg-slate-800 border border-slate-700 rounded-2xl rounded-bl-sm p-4 flex gap-1.5 items-center">
                  <div className="w-1.5 h-1.5 bg-slate-400 rounded-full animate-bounce"></div>
                  <div className="w-1.5 h-1.5 bg-slate-400 rounded-full animate-bounce" style={{ animationDelay: '0.15s' }}></div>
                  <div className="w-1.5 h-1.5 bg-slate-400 rounded-full animate-bounce" style={{ animationDelay: '0.3s' }}></div>
                </div>
              </div>
            )}
            <div ref={chatEndRef} />
          </div>

          <div className="p-4 bg-slate-900 border-t border-slate-800 shrink-0">
            <form onSubmit={handleAskAi} className="relative">
              <input 
                suppressHydrationWarning
                type="text"
                value={question} 
                onChange={e => setQuestion(e.target.value)} 
                placeholder="Ex: Qual o faturamento total pendente?"
                disabled={askAi.isPending}
                className="w-full bg-slate-950 border border-slate-700 rounded-full pl-5 pr-12 py-3 text-sm text-slate-200 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 outline-none transition-all placeholder-slate-600 disabled:opacity-50"
              />
              <button 
                suppressHydrationWarning
                type="submit" 
                disabled={askAi.isPending || !question.trim()} 
                className="absolute right-1.5 top-1.5 bottom-1.5 aspect-square bg-indigo-600 text-white rounded-full flex items-center justify-center hover:bg-indigo-500 disabled:opacity-50 transition-colors"
              >
                <svg className="w-4 h-4 translate-x-[-1px] translate-y-[1px]" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" /></svg>
              </button>
            </form>
          </div>
        </div>
        
      </div>
    </div>
  );
}

module API
	class Dashboard < Grape::API

		desc 'Get overall stat figures'
		get :dashboard do
			
			data = {
				players: Player.count,
				games: Game.count,
				stats: PlayerStat.count + GoalieStat.count
			}

			present data
		end

	end
end
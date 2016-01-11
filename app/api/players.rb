module API
	class Players < Grape::API

		resource :players do

			desc 'Search for players'
			params do
				optional :query, type: String, default: ''
				optional :team, type: String
				optional :position, type: Integer, values: Enums::Position.list
			end
			get do

				# find players
				players = Player.where 'name like ?', "%#{params[:query]}%"

				# filter by team if provided
				players = players.where team_id: Team.by_abbrev(params[:team]) if params[:team]

				# filter by position if provided
				players = players.where position: params[:position] if params[:position]

				# show results
				present :players, players
			end

			route_param :key do

				desc 'Get player information for key'
				get do

					# find player by key
					player = Player.find_by key: params[:key]
					not_found! '404.1', 'Player was not found' unless player

					# show player
					present :player, player
				end

			end
		end
	end
end

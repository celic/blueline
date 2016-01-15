module API
	class Players < Grape::API

		represent Player, with: API::Entities::Player

		resource :players do

			desc 'Search for players'
			params do
				optional :query, type: String, default: ''
				optional :team_id, type: Integer
				optional :position, type: Integer, values: Enums::Position.list
			end
			get do

				# find players
				players = Player.where 'lower(name) like ?', "%#{params[:query].downcase}%"

				# filter by team if provided
				players = players.where team_id: params[:team_id] if params[:team_id]

				# filter by position if provided
				players = players.where position: params[:position] if params[:position]

				# show results
				present :players, players
			end

			route_param :id do

				desc 'Get player information'
				get do

					# find player
					player = Player.find_by id: params[:id]
					not_found! '404.1', 'Player was not found' unless player

					# show player
					present :player, player, team: true
				end

			end

			route_param :key do

				desc 'Get player information for key'
				get do

					# find player by key
					player = Player.find_by key: params[:key]
					not_found! '404.1', 'Player was not found' unless player

					# show player
					present :player, player, team: true
				end

			end
		end
	end
end
